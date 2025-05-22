using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CsvHelper;
using MedicalImageAI.Api.Models;
using MedicalImageAI.Api.Services;
using MedicalImageAI.Api.BackgroundServices.Interfaces;
using MedicalImageAI.Api.Data;
using MedicalImageAI.Api.Entities;

using MedicalImageAI.Api.Services.Interfaces;

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
        Guid jobId = Guid.Empty;

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

            var newJob = new ImageAnalysisJob
            {
                OriginalFileName = imageFile.FileName,
                BlobUri = baseBlobUri,
            };

            try
            {
                _dbContext.ImageAnalysisJobs.Add(newJob);
                await _dbContext.SaveChangesAsync();
                jobId = newJob.Id;
                _logger.LogInformation("Created initial job record with ID: {JobId} for blob {BlobUri}", jobId, baseBlobUri);
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to save initial job record to database for blob {BlobUri}", baseBlobUri);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create analysis job record.");
            }

            Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) =>
            {
                _logger.LogInformation("Background task started for JobId: {JobId}, Blob: {BlobUri}", jobId, baseBlobUri);
                
                // This will hold the result from Custom Vision, and then we'll add OCR text to it.
                AnalysisResult? combinedAnalysisResult = null; 
                string? ocrTextFromService = null; // To store text from OCR service for the dedicated DB column
                string finalStatus = "Failed"; // Default to Failed
                string? analysisJsonToSave = null; // JSON string for the AnalysisResultJson DB column

                var scopedDbContext = sp.GetRequiredService<ApplicationDbContext>();
                var scopedVisionService = sp.GetRequiredService<ICustomVisionService>();
                var scopedOcrService = sp.GetRequiredService<IOcrService>(); // Resolve IOcrService

                ImageAnalysisJob? jobToProcess = null;

                try
                {
                    // 1. Fetch job and mark as Processing
                    jobToProcess = await scopedDbContext.ImageAnalysisJobs.FindAsync(new object[] { jobId }, ct);
                    if (jobToProcess == null)
                    {
                        _logger.LogError("Job record {JobId} not found when attempting to start processing.", jobId);
                        return; 
                    }
                    jobToProcess.Status = "Processing";
                    jobToProcess.ProcessingStartedTimestamp = DateTime.UtcNow;
                    await scopedDbContext.SaveChangesAsync(ct); // Save "Processing" state
                    _logger.LogInformation("Job {JobId} status updated to Processing.", jobId);

                    // 2. Perform Custom Vision (Classification) Analysis
                    combinedAnalysisResult = await scopedVisionService.AnalyzeImageAsync(blobUriWithSas); // This has Predictions, ErrorMessage, Success
                    _logger.LogInformation("Custom Vision analysis finished for JobId {JobId}. CV Success: {CVSuccess}", jobId, combinedAnalysisResult?.Success);

                    // 3. Perform OCR Analysis
                    try
                    {
                        _logger.LogInformation("Attempting OCR for JobId {JobId}", jobId);
                        ocrTextFromService = await scopedOcrService.ExtractTextFromImageUrlAsync(blobUriWithSas);
                        if (!string.IsNullOrEmpty(ocrTextFromService))
                        {
                            _logger.LogInformation("OCR successful for JobId {JobId}. Extracted text length: {Length}", jobId, ocrTextFromService.Length);
                        }
                        else
                        {
                            _logger.LogInformation("OCR for JobId {JobId} returned no text or an empty string.", jobId);
                        }
                    }
                    catch (Exception ocrEx)
                    {
                        _logger.LogError(ocrEx, "OCR analysis sub-task failed for JobId {JobId}. OCR text will be null.", jobId);
                        // ocrTextFromService remains null. This doesn't necessarily fail the whole job if CV was okay.
                    }

                    // 4. Populate OcrText in the combinedAnalysisResult object
                    if (combinedAnalysisResult != null) // Should be instantiated by CustomVisionService
                    {
                        combinedAnalysisResult.OcrText = ocrTextFromService;
                    }
                    else // Fallback if CustomVisionService somehow returned null
                    {
                         _logger.LogWarning("CustomVisionService.AnalyzeImageAsync returned null for JobId {JobId}. Creating new AnalysisResult.", jobId);
                        combinedAnalysisResult = new AnalysisResult { 
                            Timestamp = DateTime.UtcNow, 
                            Predictions = new List<PredictionModel>(),
                            OcrText = ocrTextFromService,
                            ErrorMessage = "Custom Vision analysis did not produce a result."
                        };
                    }
                    
                    // 5. Determine final status and serialize the (now combined) result for DB
                    finalStatus = combinedAnalysisResult.Success ? "Completed" : "Failed"; // Primarily based on CV success
                    analysisJsonToSave = JsonSerializer.Serialize(
                        combinedAnalysisResult, 
                        new JsonSerializerOptions { WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }
                    );
                    
                    _logger.LogInformation("Background processing logic complete for JobId {JobId}. Final determined status: {Status}", jobId, finalStatus);
                }
                catch (Exception taskEx) // Catches errors from CV service call or DB updates within the main try
                {
                    _logger.LogError(taskEx, "Error during main background analysis task processing for JobId {JobId}", jobId);
                    finalStatus = "Failed";

                    // Create or update an AnalysisResult to store the error for JSON serialization
                    if (combinedAnalysisResult == null)
                    {
                        combinedAnalysisResult = new AnalysisResult { Timestamp = DateTime.UtcNow, Predictions = new List<PredictionModel>() };
                    }
                    combinedAnalysisResult.ErrorMessage = combinedAnalysisResult.ErrorMessage ?? $"Background task error: {taskEx.Message}";
                    combinedAnalysisResult.OcrText = ocrTextFromService; // Include any OCR text obtained before the error
                    analysisJsonToSave = JsonSerializer.Serialize(combinedAnalysisResult);
                }
                finally 
                {
                    try
                    {
                        // Re-fetch jobToProcess ONLY if it's null and we need to ensure we have the latest for update.
                        // If it was fetched and updated to "Processing", it should still be tracked by scopedDbContext.
                        if (jobToProcess == null && jobId != Guid.Empty) 
                        {
                            jobToProcess = await scopedDbContext.ImageAnalysisJobs.FindAsync(new object[] { jobId }, ct);
                        }

                        if (jobToProcess != null)
                        {
                            jobToProcess.Status = finalStatus;
                            jobToProcess.AnalysisResultJson = analysisJsonToSave; // Contains CV + OCR + Error (if any)
                            jobToProcess.OcrResultText = ocrTextFromService;      // Store raw OCR text in dedicated column
                            jobToProcess.CompletedTimestamp = DateTime.UtcNow;
                            await scopedDbContext.SaveChangesAsync(ct);
                            _logger.LogInformation("Successfully updated job record {JobId} with final status {Status}. OCR text length: {Length}", 
                                jobId, finalStatus, jobToProcess.OcrResultText?.Length ?? 0);
                        }
                        else
                        {
                            _logger.LogError("Job record {JobId} not found for final update.", jobId);
                        }
                    }
                    catch (Exception finalDbEx)
                    {
                        _logger.LogCritical(finalDbEx, "CRITICAL: Failed to update job record {JobId} with final status {Status} after analysis attempt.", jobId, finalStatus);
                    }
                }
            };

            await _backgroundQueue.QueueBackgroundWorkItemAsync(workItem);
            _logger.LogInformation("Analysis task queued for JobId: {JobId}", jobId);

            var acceptedResponse = new UploadAcceptedResponse
            {
                Message = "File uploaded successfully. Analysis queued.",
                JobId = jobId,
                FileName = uniqueBlobName,
                BlobUri = baseBlobUri 
            };
            return Accepted(acceptedResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outer error in upload process for file {FileName}.", imageFile?.FileName ?? "N/A");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred during the upload process.");
        }
    }

    [HttpGet("{jobId}/analysis", Name = "GetAnalysisStatus")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AnalysisStatusResponse))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAnalysisStatusAsync(Guid jobId)
    {
        _logger.LogInformation("Request received for analysis status of JobId: {JobId}", jobId);

        try
        {
            var job = await _dbContext.ImageAnalysisJobs.FindAsync(jobId);

            if (job == null)
            {
                _logger.LogWarning("JobId {JobId} not found.", jobId);
                return NotFound(new { Message = $"Job with ID {jobId} not found." });
            }

            AnalysisResult? analysisResult = null;
            if (job.Status == "Completed" && !string.IsNullOrEmpty(job.AnalysisResultJson))
            {
                try
                {
                    analysisResult = JsonSerializer.Deserialize<AnalysisResult>(
                        job.AnalysisResultJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Failed to deserialize AnalysisResultJson for JobId {JobId}", jobId);
                    // Optionally, we could set job.Status to "FailedDeserialization" or similar here
                    // and return an error, or just return the job with a null analysis.
                    // For now, just reflect that analysis wasn't retrievable via the status.
                    job.Status = "ErrorDeserializingResult"; // Temporary status to indicate issue
                }
            }
            else if (job.Status == "Failed" && !string.IsNullOrEmpty(job.AnalysisResultJson))
            {
                // If it failed, AnalysisResultJson might contain an error message
                try
                {
                    analysisResult = JsonSerializer.Deserialize<AnalysisResult>(
                        job.AnalysisResultJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Failed to deserialize AnalysisResultJson (for failed job) for JobId {JobId}", jobId);
                    analysisResult = new AnalysisResult { ErrorMessage = "Could not parse stored error details." };
                }
            }

            var response = new AnalysisStatusResponse
            {
                JobId = job.Id,
                Status = job.Status,
                UploadTimestamp = job.UploadTimestamp,
                ProcessingStartedTimestamp = job.ProcessingStartedTimestamp,
                CompletedTimestamp = job.CompletedTimestamp,
                Analysis = analysisResult!, // Will be null if not completed or if deserialization fails
                OriginalFileName = job.OriginalFileName,
                BlobUri = job.BlobUri
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analysis status for JobId {JobId}", jobId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving job status.");
        }
    }

    /// <summary>
    /// Generates a CSV report of all image analysis jobs.
    /// The report is generated in memory and returned as a downloadable CSV file.
    /// If no jobs are found, an empty CSV file is returned with a header row.
    /// The CSV file is named with a timestamp to ensure uniqueness.
    /// </summary>
    /// <returns></returns>
    [HttpGet("report/csv", Name = "GetAnalysisReportCsv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAnalysisReportCsvAsync()
    {
        _logger.LogInformation("Request received for CSV analysis report.");

        try
        {
            // --- 1. Fetch data from the database
            var jobs = await _dbContext.ImageAnalysisJobs.OrderByDescending(j => j.UploadTimestamp).ToListAsync(); // Order by most recent

            if (!jobs.Any())
            {
                // Optional: Return an empty CSV or a different response if no data
                _logger.LogInformation("No jobs found to include in the report.");
                // Return an empty CSV file
                using (var memoryStream = new MemoryStream())
                using (var writer = new StreamWriter(memoryStream, Encoding.UTF8)) // Use UTF8 for broader character support
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteHeader<ImageAnalysisReportEntry>(); // Write header even if no records
                    writer.Flush(); // Ensure writer is flushed before reading stream
                    return File(memoryStream.ToArray(), "text/csv", $"empty_analysis_report_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
                }
            }

            // --- 2. Transform data into the CSV record model (ImageAnalysisReportEntry)
            var reportEntries = new List<ImageAnalysisReportEntry>();
            foreach (var job in jobs)
            {
                reportEntries.Add(MapJobToReportEntry(job));
            }

            // --- 3. Use CsvHelper to write to a memory stream
            using (var memoryStream = new MemoryStream())
            using (var writer = new StreamWriter(memoryStream, Encoding.UTF8))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture)) // CultureInfo.InvariantCulture is good for CSVs
            {
                // Optional: Configure CsvHelper if needed (e.g., custom headers, delimiters)
                // var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { /* ... options ... */ };
                // var csv = new CsvWriter(writer, csvConfig);

                csv.WriteHeader<ImageAnalysisReportEntry>(); // Writes the header row based on property names
                await csv.NextRecordAsync(); // Moves to the next line
                await csv.WriteRecordsAsync(reportEntries); // Writes all the data records

                await writer.FlushAsync(); // Ensure all data is written to the stream
                memoryStream.Position = 0; // Reset stream position for reading

                var fileName = $"image_analysis_report_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                _logger.LogInformation("CSV report generated successfully with {RecordCount} records. Filename: {FileName}", reportEntries.Count, fileName);
                return File(memoryStream.ToArray(), "text/csv", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CSV analysis report.");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while generating the report.");
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

    /// <summary>
    /// Maps an ImageAnalysisJob entity to an ImageAnalysisReportEntry for CSV reporting.
    /// This includes deserializing the AnalysisResultJson into an AnalysisResult object.
    /// </summary>
    /// <param name="job"></param>
    /// <returns></returns>
    private ImageAnalysisReportEntry MapJobToReportEntry(ImageAnalysisJob job)
    {
        AnalysisResult? analysisResult = null;
        if (!string.IsNullOrEmpty(job.AnalysisResultJson))
        {
            try
            {
                analysisResult = JsonSerializer.Deserialize<AnalysisResult>(
                    job.AnalysisResultJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Failed to deserialize AnalysisResultJson for JobId {JobId} in CSV report.", job.Id);
            }
        }

        return new ImageAnalysisReportEntry
        {
            JobId = job.Id,
            OriginalFileName = job.OriginalFileName,
            UploadTimestamp = job.UploadTimestamp,
            Status = job.Status,
            ProcessingStartedTimestamp = job.ProcessingStartedTimestamp,
            CompletedTimestamp = job.CompletedTimestamp,
            BlobUri = job.BlobUri,

            TopPredictionTag = analysisResult?.Success == true && analysisResult.Predictions.Any()
                                ? analysisResult.Predictions.First().TagName
                                : "N/A",

            TopPredictionProbability = analysisResult?.Success == true && analysisResult.Predictions.Any()
                                        ? Math.Round(analysisResult.Predictions.First().Probability, 2) // Round for display
                                        : 0.0,

            AllPredictionsSummary = analysisResult?.Success == true && analysisResult.Predictions.Any()
                                    ? string.Join("; ", analysisResult.Predictions.Select(p => $"{p.TagName}: {Math.Round(p.Probability, 1)}%"))
                                    : (analysisResult?.ErrorMessage ?? (job.Status == "Failed" ? "Failed" : "N/A")),

            ErrorMessage = analysisResult?.Success == false
                            ? analysisResult.ErrorMessage
                            : (job.Status == "Failed" && string.IsNullOrEmpty(analysisResult?.ErrorMessage) ? "Processing Failed" : string.Empty),

            ExtractedOcrText = job.OcrResultText
        };
    }
}
