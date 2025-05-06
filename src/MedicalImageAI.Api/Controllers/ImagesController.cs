using Microsoft.AspNetCore.Mvc;
using MedicalImageAI.Api.Models;
using MedicalImageAI.Api.Services;
using MedicalImageAI.Api.BackgroundServices.Interfaces;

namespace MedicalImageAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // Sets the base route to /api/images
public class ImagesController : ControllerBase
{
    private readonly ILogger<ImagesController> _logger;
    private readonly ICustomVisionService _customVisionService;
    private readonly IBackgroundQueue<Func<IServiceProvider, CancellationToken, Task>> _backgroundQueue;
    private readonly IBlobStorageService _blobStorageService;

    public ImagesController(
        ILogger<ImagesController> logger, 
        ICustomVisionService customVisionService,
        IBlobStorageService blobStorageService,
        IBackgroundQueue<Func<IServiceProvider, CancellationToken, Task>> backgroundQueue)
    {
        _logger = logger;
        _customVisionService = customVisionService;
        _blobStorageService = blobStorageService;
        _backgroundQueue = backgroundQueue;
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
    /// and queues a background task for Custom Vision analysis.
    /// Returns a 202 Accepted response with the blob URI and a message.
    /// If any validation fails, returns a 400 Bad Request with the specific error message.
    /// If an unexpected error occurs, returns a 500 Internal Server Error.
    /// </summary>
    /// <param name="imageFile"></param>
    /// <returns></returns>
    [HttpPost("upload")] // Defines the route POST /api/images/upload
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadImageAsync(IFormFile imageFile)
    {
        // --- Validate the image file ---
        if (!TryValidateImageFile(imageFile, out var validationResult))
        {
            return validationResult; // Return the specific BadRequest
        }

        _logger?.LogInformation("Received file: {FileName}, Size: {FileSize}", imageFile.FileName, imageFile.Length);

        // --- Upload to Azure Blob Storage ---
        string blobUriWithSas = string.Empty;
        string uniqueBlobName = string.Empty;
        string baseBlobUri = string.Empty;
        try
        {
            // Call the blob storage service to upload
            using (var stream = imageFile.OpenReadStream())
            {
                (uniqueBlobName, baseBlobUri) = await _blobStorageService.UploadImageAsync(stream, imageFile.FileName, imageFile.ContentType);
            }
            _logger?.LogInformation("Upload successful via service. Base Blob URI: {BlobUri}", baseBlobUri);

            // Call the blob storage service to get SAS URI
            blobUriWithSas = await _blobStorageService.GenerateReadSasUriAsync(uniqueBlobName);
            if (string.IsNullOrEmpty(blobUriWithSas))
            {
                _logger?.LogError("Failed to generate SAS URI for blob: {BlobName}.", uniqueBlobName);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate SAS URI for analysis.");
            }
            _logger?.LogInformation("Generated SAS URI via service.");
                        
            // --- Create and dispatch the work item to the background queue for Custom Vision analysis ---
            if (!string.IsNullOrEmpty(blobUriWithSas))
            {
                // Define the work item as a Func delegate. Captures necessary variables (like blobUriWithSas).
                Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) => {
                    _logger?.LogInformation("Background task started for blob: {BlobUri}", baseBlobUri);
                    
                    // Resolve the scoped service HERE, inside the delegate
                    var scopedVisionService = sp.GetRequiredService<ICustomVisionService>();
                    
                    // Call the Custom Vision service to analyze the image
                    AnalysisResult analysisResult = await scopedVisionService.AnalyzeImageAsync(blobUriWithSas);

                    // TODO: Update database record with analysisResult and set status to Completed/Failed

                    _logger?.LogInformation("Background analysis complete for blob {BlobUri}. Success: {SuccessStatus}", baseBlobUri, analysisResult?.Success);
                    if (analysisResult?.Success == true && analysisResult.Predictions.Any())
                    {
                        _logger?.LogInformation("Top prediction: {Tag} ({Prob}%)", analysisResult.Predictions.First().TagName, analysisResult.Predictions.First().Probability);
                    } else if (analysisResult?.Success == false) {
                        _logger?.LogError("Analysis failed for {BlobUri}: {Error}", baseBlobUri, analysisResult.ErrorMessage);
                    }
                };

                // Enqueue the work item
                await _backgroundQueue.QueueBackgroundWorkItemAsync(workItem);
                _logger?.LogInformation("Analysis task queued for blob: {BlobUri}", baseBlobUri);
                // --- End queueing ---

                // Return 202 Accepted immediately. Include info needed for client to potentially check status later.
                // For now, just return the base URI and a message. DB ID would go here too.
                return Accepted(new { Message = "File uploaded successfully. Analysis queued.", FileName = uniqueBlobName, BlobUri = baseBlobUri });
            }
            else
            {
                // Handle case where SAS URI couldn't be generated
                return StatusCode(StatusCodes.Status500InternalServerError, "File uploaded but could not prepare for analysis.");
            }

            // TODO: 4. Save analysis metadata (blob URI, etc.) to Database
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error uploading file {FileName} to Blob Storage.", imageFile.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while uploading the file for analysis.");
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
