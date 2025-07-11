using System.Net;
using System.Text.Json;

namespace MedicalImageAI.Api.Middleware;
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// Middleware to handle exceptions globally.
    /// This middleware catches unhandled exceptions thrown by the application,
    /// logs the error, and returns a standardized JSON response to the client.
    /// The response includes a status code, a message, and a trace ID for tracking.
    /// In development mode, it also includes detailed error information.
    /// In production mode, it provides a generic error message to avoid exposing sensitive information.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Call the next middleware in the pipeline
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception has occurred: {Message}", ex.Message);

            // Set response properties
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError; // 500

            object errorResponse; // Create a response object
            if (_env.IsDevelopment()) // In development, include more details; otherwise, a generic message
            {
                errorResponse = new
                {
                    statusCode = context.Response.StatusCode,
                    message = "An internal server error occurred. Please try again later.",
                    detailed = ex.Message, // Or ex.ToString() for full stack trace
                    traceId = context.TraceIdentifier
                };
            }
            else // Production or other environments
            {
                errorResponse = new
                {
                    statusCode = context.Response.StatusCode,
                    message = "An internal server error occurred. Please try again later.",
                    traceId = context.TraceIdentifier
                };
            }

            // Serialize and write the response
            var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase // Ensure camelCase for JSON properties
            });
            await context.Response.WriteAsync(jsonResponse);
        }
    }
}
