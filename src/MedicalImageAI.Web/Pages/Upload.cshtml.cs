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

            // --- Add API Key to the request headers ---
            var apiKey = Configuration["ApiClientSettings:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("API Key for ApiClientSettings:ApiKey is not configured in the Web App.");
                ErrorMessage = "Critical configuration error: API Key for backend service is missing.";
                return Page();
            }
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

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

    /// <summary>
    /// Handles the request to check the analysis status of a job.
    /// This method is called via AJAX from the client-side JavaScript.
    /// It proxies the request to the API and returns the status of the analysis.
    /// </summary>
    /// <param name="jobId"></param>
    /// <returns></returns>
    public async Task<JsonResult> OnGetAnalysisStatusAsync(Guid jobId)
    {
        _logger.LogInformation("Proxying request for analysis status of JobId: {JobId}", jobId);

        var apiBaseUrl = Configuration["ApiSettings:BaseUrl"];
        var apiKey = Configuration["ApiClientSettings:ApiKey"];

        if (string.IsNullOrEmpty(apiBaseUrl))
        {
            _logger.LogError("API Base URL (ApiSettings:BaseUrl) is not configured in the Web App.");
            return new JsonResult(new { error = "Server configuration error: API endpoint not specified." })
                { StatusCode = StatusCodes.Status500InternalServerError };
        }
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("API Key (ApiClientSettings:ApiKey) is not configured in the Web App.");
            return new JsonResult(new { error = "Server configuration error: API key for backend service is missing." })
                { StatusCode = StatusCodes.Status500InternalServerError };
        }

        var actualApiStatusEndpoint = $"{apiBaseUrl.TrimEnd('/')}/api/images/{jobId}/analysis";
        _logger.LogInformation("Proxy target URL: {TargetUrl}", actualApiStatusEndpoint);

        try
        {
            var client = _httpClientFactory.CreateClient("ApiClient");

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await client.GetAsync(actualApiStatusEndpoint);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponseString = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Successfully proxied status request for JobId {JobId}. API Payload received.", jobId);

                // Return a JsonResult, which will re-serialize the string.
                // We could also have sent the raw string with ContentResult.
                AnalysisStatusResponse? apiData = JsonSerializer.Deserialize<AnalysisStatusResponse>(jsonResponseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (apiData == null)
                {
                    _logger.LogError("Deserialization of API response for JobId {JobId} returned null.", jobId);
                    return new JsonResult(new { error = "Failed to parse API response." })
                        { StatusCode = StatusCodes.Status500InternalServerError };
                }

                return new JsonResult(apiData);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Proxied API request for JobId {JobId} failed. Status: {StatusCode}, Details: {ErrorContent}",
                    jobId, response.StatusCode, errorContent);
                // Forward a similar error structure
                return new JsonResult(new { error = $"API returned error: {response.StatusCode}", details = errorContent })
                    { StatusCode = (int)response.StatusCode };
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "Network error proxying API request for JobId {JobId} to {StatusEndpoint}", jobId, actualApiStatusEndpoint);
            return new JsonResult(new { error = "Network error during proxy to backend API." })
            { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error proxying API request for JobId {JobId}", jobId);
            return new JsonResult(new { error = "Unexpected server error while proxying request." })
            { StatusCode = StatusCodes.Status500InternalServerError };
        }
    }

    /// <summary>
    /// Handles the download of the CSV report from the API.
    /// This method is called when the user clicks the download link/button.
    /// The CSV report is generated by the API and returned as a file download.
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> OnGetDownloadCsvReportAsync() // Using OnGet for a simple download link/button
    {
        _logger.LogInformation("--- DEBUG: OnGetDownloadCsvReportAsync handler CALLED. ---");
        
        _logger.LogInformation("User requested CSV report download.");
        ErrorMessage = null; // Clear previous errors from the PageResponse property if needed

        var apiBaseUrl = Configuration["ApiSettings:BaseUrl"];
        var apiKey = Configuration["ApiClientSettings:ApiKey"];

        if (string.IsNullOrEmpty(apiBaseUrl) || string.IsNullOrEmpty(apiKey))
        {
            ErrorMessage = "API configuration (BaseUrl or ApiKey) is missing in the Web App for report download.";
            _logger.LogError(ErrorMessage);
            // Possible TODO: Redirect to an error page or display this message prominently
            // For now, returning the current page will show ErrorMessage
            return Page();
        }

        var reportEndpoint = $"{apiBaseUrl.TrimEnd('/')}/api/images/report/csv";
        _logger.LogInformation("Web App attempting to fetch CSV report from API: {ReportEndpoint}", reportEndpoint);

        try
        {
            var client = _httpClientFactory.CreateClient("ApiClient");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

            var response = await client.GetAsync(reportEndpoint);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully fetched CSV report from API. Status: {StatusCode}", response.StatusCode);
                var fileBytes = await response.Content.ReadAsByteArrayAsync();

                // Try to get filename from Content-Disposition header sent by API
                var contentDisposition = response.Content.Headers.ContentDisposition;
                var fileName = contentDisposition?.FileNameStar ??
                               contentDisposition?.FileName ??
                               $"image_analysis_report_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

                return File(fileBytes, "text/csv", fileName); // This triggers the download in the browser
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                ErrorMessage = $"Failed to download CSV report from API. Status: {response.StatusCode}. Details: {errorContent}";
                _logger.LogError("API call to fetch CSV report failed. Status: {StatusCode}, API Error: {ErrorContent}", response.StatusCode, errorContent);
                return Page(); // Redisplay current page with ErrorMessage
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"An unexpected error occurred while trying to download the report: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during CSV report download proxy.");
            return Page(); // Redisplay current page with ErrorMessage
        }
    }
}
