namespace MedicalImageAI.Api.Services.Interfaces;

public interface IOcrService
{
    /// <summary>
    /// Extracts text from an image using the specified image URL.
    /// </summary>
    /// <param name="imageUrl">The publicly accessible URL of the image to analyze.</param>
    /// <returns>A string containing the extracted text, or null/empty if no text found or an error occurred.</returns>
    Task<string>ExtractTextFromImageUrlAsync(string imageUrl);
}
