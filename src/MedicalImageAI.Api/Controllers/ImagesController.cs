using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models; // For BlobHttpHeaders
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // Optional: for logging
using System;
using System.IO; // Required for Path
using System.Threading.Tasks;
using Azure.Storage.Sas;
using MedicalImageAI.Api.Models;
using MedicalImageAI.Api.Services;

namespace MedicalImageAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // Sets the base route to /api/images
public class ImagesController : ControllerBase
{
    private readonly ILogger<ImagesController> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName = "uploaded-images";
    private readonly ICustomVisionService _customVisionService;

    public ImagesController(ILogger<ImagesController> logger, BlobServiceClient blobServiceClient, ICustomVisionService customVisionService)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
        _customVisionService = customVisionService;
    }

    [HttpGet("ping")] // Defines GET /api/images/ping
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        _logger?.LogInformation("Ping endpoint hit!");
        return Ok("Pong from API");
    }

    [HttpPost("upload")] // Defines the route POST /api/images/upload
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImageAsync(IFormFile imageFile)
    {
        // --- Basic Validation ---
        if (imageFile == null || imageFile.Length == 0)
        {
            _logger?.LogWarning("Upload attempt with no file."); // Optional logging
            return BadRequest("No file uploaded.");
        }

        // Optional: Basic check for allowed file types (more robust checks exist)
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp" }; // DICOM needs special handling later
        var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
        {
            _logger?.LogWarning("Upload attempt with invalid file type: {FileName}", imageFile.FileName);
            return BadRequest($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");
        }

        // Optional: Check file size (e.g., limit to 4MB, Custom Vision has limits)
        long maxFileSize = 4 * 1024 * 1024; // 4 MB
        if (imageFile.Length > maxFileSize)
        {
                _logger?.LogWarning("Upload attempt with file exceeding size limit: {FileName}, Size: {FileSize}", imageFile.FileName, imageFile.Length);
            return BadRequest($"File size exceeds the limit of {maxFileSize / 1024 / 1024} MB.");
        }

        _logger?.LogInformation("Received file: {FileName}, Size: {FileSize}", imageFile.FileName, imageFile.Length);

        // --- Upload to Azure Blob Storage ---
        try
        {
            // Reference to the container
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            // Create the container if it doesn't exist
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None); // Use PublicAccessType.None for private blobs

            // Generate a unique blob name to avoid overwrites
            var uniqueBlobName = $"{Guid.NewGuid()}{ext}"; // e.g., "guid.png"

            // Reference to the blob
            BlobClient blobClient = containerClient.GetBlobClient(uniqueBlobName);

            // Upload the file stream to the blob
            _logger?.LogInformation("Uploading to Blob Storage as blob: {BlobName}", uniqueBlobName);
            using (var stream = imageFile.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = imageFile.ContentType });
            }
            _logger?.LogInformation("Upload successful. Blob URI: {BlobUri}", blobClient.Uri);


            // TODO: 3. Trigger Custom Vision analysis using blobClient.Uri.ToString()
            // TODO: 4. Save analysis metadata (blob URI, etc.) to Database




            // ---> GENERATE SAS TOKEN FOR THE UPLOADED BLOB <---
            string blobUriWithSas = string.Empty;
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
            // ---> END SAS TOKEN GENERATION <---









            
            // --- Call Custom Vision Service ---
            // string blobUri = blobClient.Uri.ToString();
            string blobUri = blobUriWithSas;
            AnalysisResult? analysisResult = null;
            try
            {
                analysisResult = await _customVisionService.AnalyzeImageAsync(blobUri);
            }
            catch (Exception serviceEx) // Catch potential errors from the service call itself
            {
                _logger?.LogError(serviceEx, "Error calling CustomVisionService for Blob URI {BlobUri}", blobUri);
                // Decide how to handle - maybe return a specific error response
            }


            
            // Return the Blob URI + any relevant info
            var response = new UploadResponse
            {
                FileName = uniqueBlobName,
                BlobUri = blobUri,
                Message = $"File '{imageFile.FileName}' uploaded successfully as '{uniqueBlobName}'. Analysis performed.",
                Analysis = analysisResult
            };


            return Ok(response);

        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error uploading file {FileName} to Blob Storage.", imageFile.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while uploading the file.");
        }
    }
}
