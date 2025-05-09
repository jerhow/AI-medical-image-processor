using Microsoft.AspNetCore.Mvc;
using MedicalImageAI.Api.Models;
using MedicalImageAI.Api.Services;
using MedicalImageAI.Api.BackgroundServices.Interfaces;
using MedicalImageAI.Api.Data;
using MedicalImageAI.Api.Entities;
using System.Text.Json;

namespace MedicalImageAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // Sets the base route to /api/images
public class ImagesController : ControllerBase
{
    private readonly ILogger<ImagesController> _logger;
    private readonly IBackgroundQueue<Func<IServiceProvider, CancellationToken, Task>> _backgroundQueue;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ApplicationDbContext _dbContext;

    public ImagesController(
        ILogger<ImagesController> logger, 
        ICustomVisionService customVisionService,
        IBlobStorageService blobStorageService,
        ApplicationDbContext dbContext,
        IBackgroundQueue<Func<IServiceProvider, CancellationToken, Task>> backgroundQueue)
    {
        _logger = logger;
        _blobStorageService = blobStorageService;
        _backgroundQueue = backgroundQueue;
        _dbContext = dbContext;
    }

    [HttpGet("ping")] // Defines GET /api/images/ping
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        _logger?.LogInformation("Ping endpoint hit!");
        return Ok("Pong from API");
    }

    /// <summary>
    /// Controller action to handle image uploads.
    /// Validates the file, uploads it to Azure Blob Storage, generates a SAS URI for analysis,
    /// queues a background task (`ImageAnalysisJob`) for Custom Vision analysis, and marks the job as "Queued" in the database.
    /// /// Returns a 202 Accepted response with the blob URI and a message.
    /// The queued `ImageAnalysisJob` will also update its DB status to "Processing" when the background task starts and to "Completed" or "Failed" based on the analysis result.
    /// </summary>
    /// <param name="imageFile"></param>
    /// <returns></returns>
    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(UploadAcceptedResponse))] // Specify response type
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadImageAsync(IFormFile imageFile)
    {
        if (!TryValidateImageFile(imageFile, out var validationResult))
        {
            return validationResult;
        }

        _logger.LogInformation("Received valid file: {FileName}, Size: {FileSize}", imageFile.FileName, imageFile.Length);

        string uniqueBlobName = string.Empty;
        string baseBlobUri = string.Empty;
        string blobUriWithSas = string.Empty;
        Guid jobId = Guid.Empty; // The DB record ID

        try
        {
            using (var stream = imageFile.OpenReadStream())
            {
                (uniqueBlobName, baseBlobUri) = await _blobStorageService.UploadImageAsync(stream, imageFile.FileName, imageFile.ContentType);
            }
            _logger.LogInformation("Upload successful via service. Base Blob URI: {BlobUri}", baseBlobUri);

            blobUriWithSas = await _blobStorageService.GenerateReadSasUriAsync(uniqueBlobName);
            if (string.IsNullOrEmpty(blobUriWithSas))
            {
                _logger.LogError("Failed to generate SAS URI for blob: {BlobName}.", uniqueBlobName);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate SAS URI for analysis.");
            }
            _logger.LogInformation("Generated SAS URI via service.");

            // --- Create and save initial DB record ---
            var newJob = new ImageAnalysisJob
            {
                // NOTE: Constructor sets Id, UploadTimestamp, and default Status ("Queued")
                OriginalFileName = imageFile.FileName,
                BlobUri = baseBlobUri, // Base URI (without SAS)
            };

            try
            {
                _dbContext.ImageAnalysisJobs.Add(newJob); // Use Add, AddAsync is usually for special cases
                await _dbContext.SaveChangesAsync();
                jobId = newJob.Id; // Get the generated ID
                _logger.LogInformation("Created initial job record with ID: {JobId} for blob {BlobUri}", jobId, baseBlobUri);
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to save initial job record to database for blob {BlobUri}", baseBlobUri);
                // TODO: Delete the uploaded blob here to avoid orphaned files
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create analysis job record.");
            }
            // --- /Create and save initial DB record ---

            // --- Dispatch work item to the background queue ---
            // Defines the work item as a Func delegate. Captures necessary variables.
            Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) =>
            {
                _logger.LogInformation("Background task started for JobId: {JobId}, Blob: {BlobUri}", jobId, baseBlobUri);
                AnalysisResult analysisResult = new AnalysisResult();
                string finalStatus = "Failed";
                string analysisJson = string.Empty;

                // Resolve scoped services HERE, inside the delegate
                var scopedDbContext = sp.GetRequiredService<ApplicationDbContext>();
                var scopedVisionService = sp.GetRequiredService<ICustomVisionService>();

                ImageAnalysisJob? jobToProcess = null;
                try
                {
                    // Mark as Processing in DB
                    jobToProcess = await scopedDbContext.ImageAnalysisJobs.FindAsync(new object[] { jobId }, ct);
                    if (jobToProcess == null)
                    {
                        _logger.LogError("Job record {JobId} not found when attempting to start processing.", jobId);
                        return; // Exit task if job not found
                    }
                    jobToProcess.Status = "Processing";
                    jobToProcess.ProcessingStartedTimestamp = DateTime.UtcNow;
                    await scopedDbContext.SaveChangesAsync(ct);
                    _logger.LogInformation("Job {JobId} status updated to Processing.", jobId);

                    // Call the Custom Vision service to analyze the image
                    analysisResult = await scopedVisionService.AnalyzeImageAsync(blobUriWithSas); // Use SAS URI here

                    // Prepare results for DB
                    analysisJson = JsonSerializer.Serialize(analysisResult, new JsonSerializerOptions { WriteIndented = false });
                    finalStatus = analysisResult?.Success == true ? "Completed" : "Failed";
                    _logger.LogInformation("Background analysis finished for JobId {JobId}. Status: {Status}", jobId, finalStatus);
                }
                catch (Exception taskEx)
                {
                    _logger.LogError(taskEx, "Error during background analysis task for JobId {JobId}", jobId);
                    finalStatus = "Failed";
                    // Serialize a minimal error object if analysisResult is null
                    analysisJson = JsonSerializer.Serialize(new AnalysisResult { ErrorMessage = $"Background task failed: {taskEx.Message}", Predictions = new List<PredictionModel>() });
                }
                finally // Ensure DB is updated with the final status and result
                {
                    try
                    {
                        // Re-fetch or use existing jobToProcess if still valid and DB context is not disposed by error
                        if (jobToProcess == null)
                        {
                            jobToProcess = await scopedDbContext.ImageAnalysisJobs.FindAsync(new object[] { jobId }, ct);
                        }
                        
                        if (jobToProcess != null)
                        {
                            jobToProcess.Status = finalStatus;
                            jobToProcess.AnalysisResultJson = analysisJson;
                            jobToProcess.CompletedTimestamp = DateTime.UtcNow;
                            await scopedDbContext.SaveChangesAsync(ct);
                            _logger.LogInformation("Successfully updated job record {JobId} with final status {Status}", jobId, finalStatus);
                        }
                        else
                        {
                            _logger.LogError("Job record {JobId} not found for final update. This should not happen if initial save succeeded.", jobId);
                        }
                    }
                    catch (Exception finalDbEx)
                    {
                        _logger.LogCritical(finalDbEx, "CRITICAL: Failed to update job record {JobId} with final status {Status} after analysis attempt.", jobId, finalStatus);
                        // This is a critical failure. The job processed, but its final state couldn't be saved.
                        // TODO: May add this to a dead-letter queue or some other form of alerting.
                    }
                }
            };

            await _backgroundQueue.QueueBackgroundWorkItemAsync(workItem);
            _logger.LogInformation("Analysis task queued for JobId: {JobId}", jobId);

            // Return 202 Accepted with the Job ID and other relevant info
            var acceptedResponse = new UploadAcceptedResponse
            {
                Message = "File uploaded successfully. Analysis queued.",
                JobId = jobId,
                FileName = uniqueBlobName, // The unique name given by the server
                BlobUri = baseBlobUri    // The base URI of the blob
            };
            
            // Optionally generate a status check URL when we have that endpoint ready:
            // string statusUrl = Url.ActionLink(nameof(GetAnalysisStatus), "Images", new { jobId = jobId });
            // return Accepted(statusUrl, acceptedResponse);
            return Accepted(acceptedResponse); // Simplified for now
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outer error in upload process for file {FileName}.", imageFile?.FileName ?? "N/A");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred during the upload process.");
        }
    }

    /// <summary>
    /// Validates the uploaded image file.
    /// Checks for null, empty file, valid file type, and size.
    /// If any validation fails, sets the errorResult to a BadRequest result with an appropriate message.
    /// Returns true if the file is valid, false otherwise.
    /// </summary>
    /// <param name="imageFile"></param>
    /// <param name="errorResult"></param>
    /// <returns></returns>
    private bool TryValidateImageFile(IFormFile imageFile, out IActionResult errorResult)
    {
        errorResult = Ok(); // Default to no error

        if (imageFile == null || imageFile.Length == 0)
        {
            _logger?.LogWarning("Upload attempt with no file.");
            errorResult = BadRequest("No file uploaded.");
            return false;
        }

        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp" }; // DICOM needs special handling later if we end up supporting it
        string ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
        {
            _logger?.LogWarning("Upload attempt with invalid file type: {FileName}", imageFile.FileName);
            errorResult = BadRequest($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");
            return false;
        }

        // Check file size (e.g., limit to 4MB, Custom Vision has limits)
        long maxFileSize = 4 * 1024 * 1024; // 4 MB
        if (imageFile.Length > maxFileSize)
        {
            _logger?.LogWarning("Upload attempt with file exceeding size limit: {FileName}, Size: {FileSize}", imageFile.FileName, imageFile.Length);
            errorResult = BadRequest($"File size exceeds the limit of {maxFileSize / 1024 / 1024} MB.");
            return false;
        }

        return true; // File is valid
    }
}
