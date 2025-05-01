using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // Optional: for logging
using System;
using System.IO; // Required for Path
using System.Threading.Tasks;

namespace MedicalImageAI.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Sets the base route to /api/images
    public class ImagesController : ControllerBase
    {
        private readonly ILogger<ImagesController> _logger; // Optional: for logging

        // Constructor for dependency injection (add ILogger if needed)
        public ImagesController(ILogger<ImagesController> logger)
        {
            _logger = logger;
        }

        [HttpGet("ping")] // Defines GET /api/images/ping
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Ping()
        {
            _logger?.LogInformation("Ping endpoint hit!"); // Add log
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
                // --- Placeholder for future logic ---
                // TODO: 1. Generate a unique file name (to avoid collisions)
                // TODO: 2. Upload the imageFile.OpenReadStream() to Azure Blob Storage
                // TODO: 3. Trigger Custom Vision analysis (likely asynchronously) using the Blob URI
                // TODO: 4. Save analysis metadata (blob URI, etc.) to Database
                // TODO: 5. Return appropriate response (e.g., analysis ID or initial result)

                // For now, just return OK indicating the file was received
                await Task.Delay(10); // Simulate async work (remove later)
                return Ok(new { message = $"File '{imageFile.FileName}' received successfully. Processing started." });
            }
            catch (Exception ex)
            {
                 _logger?.LogError(ex, "Error processing uploaded file: {FileName}", imageFile.FileName);
                // Return a generic server error
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while processing the file.");
            }
        }
    }
}
