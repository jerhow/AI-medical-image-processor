using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;
using MedicalImageAI.Web.Models;

namespace MedicalImageAI.Web.Pages;

public class UploadModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    public readonly IConfiguration Configuration;
    private readonly ILogger<UploadModel> _logger;

    [BindProperty]
    public IFormFile? UploadedImage { get; set; }

    public AcceptedUploadResponse? PageResponse { get; private set; } // Store the response from your API after a successful "202 Accepted"

    [TempData] // Use TempData so message survives a redirect if we choose to use one later
    public string? ErrorMessage { get; set; }

    public UploadModel(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<UploadModel> logger)
    {
        Configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public void OnGet()
    {
        // Add initialization logic if needed when the page is first loaded
    }

    /// <summary>
    /// Handles the form submission when the user uploads an image.
    /// Validates the file type and size, then sends it to the API for processing.
    /// Returns the page with the current model state.
    /// This method is called when the form is submitted.
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnPostAsync()
    {
        ErrorMessage = null; // Clear previous errors
        PageResponse = null; // Clear previous responses

        if (!ModelState.IsValid || UploadedImage == null || UploadedImage.Length == 0)
        {
            ModelState.AddModelError("UploadedImage", "Please select a valid image file.");
            return Page();
        }

        // Basic client-side validation for file type
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp" };
        var ext = Path.GetExtension(UploadedImage.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
        {
            ModelState.AddModelError("UploadedImage", $"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");
            return Page();
        }

        // Basic client-side validation for file size
        long maxFileSize = 4 * 1024 * 1024; // 4 MB
        if (UploadedImage.Length > maxFileSize)
        {
            ModelState.AddModelError("UploadedImage", $"File size exceeds the limit of {maxFileSize / 1024 / 1024} MB.");
            return Page();
        }

        var apiBaseUrl = Configuration["ApiSettings:BaseUrl"];
        if (string.IsNullOrEmpty(apiBaseUrl))
        {
            ErrorMessage = "API base URL is not configured. Please check application settings.";
            _logger.LogError(ErrorMessage);
            return Page();
        }
        var uploadEndpoint = $"{apiBaseUrl.TrimEnd('/')}/api/images/upload";

        _logger.LogInformation("User uploaded {FileName}. Preparing to POST to {Endpoint}", UploadedImage.FileName, uploadEndpoint);

        try
        {
            var client = _httpClientFactory.CreateClient("ApiClient"); // Named client is not yet configured in Program.cs, so we get the default one for now

            using (var content = new MultipartFormDataContent())
            using (var fileStream = UploadedImage.OpenReadStream())
            {
                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(UploadedImage.ContentType);
                // "imageFile" MUST match the parameter name in your API's UploadImageAsync method
                content.Add(streamContent, "imageFile", UploadedImage.FileName);

                HttpResponseMessage response = await client.PostAsync(uploadEndpoint, content);

                if (response.StatusCode == System.Net.HttpStatusCode.Accepted) // Specifically check for 202
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("API returned 202 Accepted. Payload: {JsonResponse}", jsonResponse);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    PageResponse = JsonSerializer.Deserialize<AcceptedUploadResponse>(jsonResponse, options);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"API request failed. Status: {response.StatusCode}. Details: {errorContent}";
                    _logger.LogError("API request failed. Status: {StatusCode}, Reason: {ReasonPhrase}, Content: {ErrorContent}",
                        response.StatusCode, response.ReasonPhrase, errorContent);
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            ErrorMessage = $"Network error connecting to API: {httpEx.Message}. Ensure the API is running and accessible at {apiBaseUrl}.";
            _logger.LogError(httpEx, "Network error calling API at {UploadEndpoint}", uploadEndpoint);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"An unexpected error occurred during upload: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during upload process for file {FileName}", UploadedImage.FileName);
        }

        return Page();
    }
}
