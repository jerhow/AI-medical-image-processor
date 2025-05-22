using Azure;
using Azure.AI.Vision.ImageAnalysis;
using System.Text;
using MedicalImageAI.Api.Services.Interfaces;

namespace MedicalImageAI.Api.Services;

public class OcrService : IOcrService
{
    private readonly ILogger<OcrService> _logger;
    private readonly ImageAnalysisClient _imageAnalysisClient;

    public OcrService(IConfiguration configuration, ILogger<OcrService> logger)
    {
        _logger = logger;
        string endpoint = configuration["CognitiveServicesVision:Endpoint"] ?? throw new ArgumentNullException("CognitiveServicesVision:Endpoint not configured");
        string key = configuration["CognitiveServicesVision:Key"] ?? throw new ArgumentNullException("CognitiveServicesVision:Key not configured");

        _imageAnalysisClient = new ImageAnalysisClient(
            new Uri(endpoint),
            new AzureKeyCredential(key));
    }

    public async Task<string> ExtractTextFromImageUrlAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            _logger.LogWarning("ExtractTextFromImageUrlAsync called with null or empty imageUrl.");
            return string.Empty;
        }

        _logger.LogInformation("Attempting OCR for image URL: {ImageUrl}", imageUrl);

        try
        {
            // Specify Read (OCR) as the visual feature to analyze
            VisualFeatures visualFeatures = VisualFeatures.Read;

            // Analyze image from URL
            ImageAnalysisResult result = await _imageAnalysisClient.AnalyzeAsync(
                new Uri(imageUrl),
                visualFeatures
            );

            if (result.Read != null && result.Read.Blocks.Count > 0)
            {
                _logger.LogInformation("OCR successful for {ImageUrl}. Extracted {BlockCount} blocks of text.", imageUrl, result.Read.Blocks.Count);
                StringBuilder extractedText = new StringBuilder();
                foreach (var block in result.Read.Blocks)
                {
                    foreach (var line in block.Lines)
                    {
                        extractedText.AppendLine(line.Text);
                    }
                }
                return extractedText.ToString().Trim();
            }
            else
            {
                _logger.LogInformation("No text blocks found by OCR for image URL: {ImageUrl}", imageUrl);
                // return string.Empty; // Or null, depending on how you want to represent "no text found"
                return "No text blocks found by OCR"; // Indicate no text found
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing OCR on image URL {ImageUrl}", imageUrl);
            return string.Empty; // Indicate an error occurred
        }
    }
}
