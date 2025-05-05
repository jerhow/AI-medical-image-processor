using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Sas;
using MedicalImageAI.Api.Models;
using MedicalImageAI.Api.Services;
using MedicalImageAI.Api.BackgroundServices.Interfaces;

namespace MedicalImageAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // Sets the base route to /api/images
public class ImagesController : ControllerBase
{
    private readonly ILogger<ImagesController> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName = "uploaded-images";
    private readonly ICustomVisionService _customVisionService;
    private readonly IBackgroundQueue<Func<IServiceProvider, CancellationToken, Task>> _backgroundQueue;

    public ImagesController(
        ILogger<ImagesController> logger, 
        BlobServiceClient blobServiceClient, 
        ICustomVisionService customVisionService,
        IBackgroundQueue<Func<IServiceProvider, CancellationToken, Task>> backgroundQueue)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
        _customVisionService = customVisionService;
        _backgroundQueue = backgroundQueue;
    }

    [HttpGet("ping")] // Defines GET /api/images/ping
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        _logger?.LogInformation("Ping endpoint hit!");
        return Ok("Pong from API");
    }

    [HttpPost("upload")] // Defines the route POST /api/images/upload
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadImageAsync(IFormFile imageFile)
    {
        // --- Basic Validation ---
        if (imageFile == null || imageFile.Length == 0)
        {
            _logger?.LogWarning("Upload attempt with no file.");
            return BadRequest("No file uploaded.");
        }

        // Basic check for allowed file types
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp" }; // DICOM needs special handling later if we end up supporting it
        var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
        {
            _logger?.LogWarning("Upload attempt with invalid file type: {FileName}", imageFile.FileName);
            return BadRequest($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");
        }

        // Check file size (e.g., limit to 4MB, Custom Vision has limits)
        long maxFileSize = 4 * 1024 * 1024; // 4 MB
        if (imageFile.Length > maxFileSize)
        {
                _logger?.LogWarning("Upload attempt with file exceeding size limit: {FileName}, Size: {FileSize}", imageFile.FileName, imageFile.Length);
            return BadRequest($"File size exceeds the limit of {maxFileSize / 1024 / 1024} MB.");
        }

        _logger?.LogInformation("Received file: {FileName}, Size: {FileSize}", imageFile.FileName, imageFile.Length);

        // --- Upload to Azure Blob Storage ---
        string blobUriWithSas = string.Empty;
        string uniqueBlobName = string.Empty;
        string baseBlobUri = string.Empty;
        try
        {
            // Reference to the container
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            // Create the container if it doesn't exist
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None); // Use PublicAccessType.None for private blobs

            // Generate a unique blob name to avoid overwrites
            uniqueBlobName = $"{Guid.NewGuid()}{ext}"; // e.g., "guid.png"

            // Reference to the blob
            BlobClient blobClient = containerClient.GetBlobClient(uniqueBlobName);

            // Upload the file stream to the blob
            _logger?.LogInformation("Uploading to Blob Storage as blob: {BlobName}", uniqueBlobName);
            using (var stream = imageFile.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = imageFile.ContentType });
            }
            baseBlobUri = blobClient.Uri.ToString();
            _logger?.LogInformation("Upload successful. Blob URI: {BlobUri}", blobClient.Uri);

            // --- Generate a SAS token for the blob to allow Custom Vision to access it ---
            if (blobClient.CanGenerateSasUri)
            {
                // Define SAS parameters
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = _containerName,
                    BlobName = uniqueBlobName,
                    Resource = "b", // "b" for blob
                    StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Allow for clock skew
                    ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5), // Grant access for 5 minutes
                    Protocol = SasProtocol.Https // Require HTTPS
                };
                sasBuilder.SetPermissions(BlobSasPermissions.Read); // Grant only Read permission

                // Generate the SAS URI
                Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
                blobUriWithSas = sasUri.ToString();
                _logger?.LogInformation("Generated SAS URI for analysis (valid for 5 mins).");
            }
            else
            {
                _logger?.LogError("Cannot generate SAS URI for blob: {BlobName}. Check credentials.", uniqueBlobName);
                // Handle error - perhaps return a specific error code/message
                // For now, we'll let it proceed and likely fail in the service call below
                blobUriWithSas = blobClient.Uri.ToString(); // Fallback to base URI (will likely fail analysis)
            }

            // --- Dispatch work item to the background queue for Custom Vision analysis ---
            if (!string.IsNullOrEmpty(blobUriWithSas))
            {
                // Define the work item as a delegate. Captures necessary variables (like blobUriWithSas).
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
}
