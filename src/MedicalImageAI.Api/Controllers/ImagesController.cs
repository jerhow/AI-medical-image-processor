using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models; // For BlobHttpHeaders
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // Optional: for logging
using System;
using System.IO; // Required for Path
using System.Threading.Tasks;
using MedicalImageAI.Api.Models;

namespace MedicalImageAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // Sets the base route to /api/images
public class ImagesController : ControllerBase
{
    private readonly ILogger<ImagesController> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName = "uploaded-images";

    // Constructor for dependency injection (add ILogger if needed)
    public ImagesController(ILogger<ImagesController> logger, BlobServiceClient blobServiceClient)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
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

            // Return the Blob URI + any relevant info
            var response = new UploadResponse
            {
                FileName = uniqueBlobName,
                BlobUri = blobClient.Uri.ToString(),
                Message = $"File '{imageFile.FileName}' uploaded successfully as '{uniqueBlobName}'."
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
