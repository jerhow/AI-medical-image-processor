namespace MedicalImageAI.Api.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private const string API_KEY_HEADER_NAME = "X-API-Key";

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    /// <summary>
    /// Middleware to validate API key from the request headers.
    /// The API key is expected to be provided in the request headers with the name "X-API-Key".
    /// If the API key is missing or invalid, a 401 Unauthorized response is returned.
    /// If the API key is valid, the request is passed to the next middleware in the pipeline.
    /// This middleware allows access to the Swagger UI and the Ping endpoint without an API key.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Allow Swagger to be accessed without an API key for development/testing
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        // Allow Ping endpoint to be accessed without an API key
        if (context.Request.Path.StartsWithSegments("/api/images/ping"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(API_KEY_HEADER_NAME, out var extractedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("API Key was not provided.");
            return;
        }

        var apiKey = _configuration["Security:ApiKey"];

        if (string.IsNullOrEmpty(apiKey) || !apiKey.Equals(extractedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized; // Or 403 Forbidden
            await context.Response.WriteAsync("Unauthorized client: Invalid API Key.");
            return;
        }

        await _next(context);
    }
}
