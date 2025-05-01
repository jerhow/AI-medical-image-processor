using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MedicalImageAI.Api.Models;

namespace MedicalImageAI.Api.Services;

public class CustomVisionService : ICustomVisionService
{
    private readonly ILogger<CustomVisionService> _logger;
    private readonly CustomVisionPredictionClient _predictionClient;
    private readonly Guid _projectId;
    private readonly string _publishedModelName;

    public CustomVisionService(IConfiguration configuration, ILogger<CustomVisionService> logger)
    {
        _logger = logger;
        
        string predictionKey = configuration["CustomVision:PredictionKey"] ?? throw new ArgumentNullException("CustomVision:PredictionKey not configured");
        string endpoint = configuration["CustomVision:PredictionEndpoint"] ?? throw new ArgumentNullException("CustomVision:PredictionEndpoint not configured");
        string projectIdString = configuration["CustomVision:ProjectId"] ?? throw new ArgumentNullException("CustomVision:ProjectId not configured");
        _publishedModelName = configuration["CustomVision:PublishedModelName"] ?? throw new ArgumentNullException("CustomVision:PublishedModelName not configured");

        if (!Guid.TryParse(projectIdString, out _projectId))
        {
            throw new ArgumentException("CustomVision:ProjectId is not a valid GUID.");
        }

        // Create and authenticate the prediction client
        _predictionClient = new CustomVisionPredictionClient(new ApiKeyServiceClientCredentials(predictionKey))
        {
            Endpoint = endpoint
        };
    }

    /// <summary>
    /// Asynchronously analyzes an image URL using the Custom Vision model.
    /// This method uses the Custom Vision Prediction API to classify the image and return predictions.
    /// NOTE: References to `PredictionModel` are fully qualified because also exists in `Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models`
    /// </summary>
    /// <param name="imageUrl"></param>
    /// <returns></returns>
    public async Task<AnalysisResult> AnalyzeImageAsync(string imageUrl)
    {
        _logger.LogInformation("Analyzing image URL: {ImageUrl} using model {ModelName}", imageUrl, _publishedModelName);

        try
        {
            ImageUrl url = new ImageUrl(imageUrl);
            ImagePrediction predictionResult = await _predictionClient.ClassifyImageUrlAsync(_projectId, _publishedModelName, url);

            // Process the results
            var analysisResult = new AnalysisResult
            {
                Timestamp = DateTime.UtcNow,
                Predictions = predictionResult.Predictions.Select(p => new MedicalImageAI.Api.Models.PredictionModel
                {
                    TagName = p.TagName,
                    Probability = p.Probability * 100 // Convert to percentage
                }).OrderByDescending(p => p.Probability).ToList() // Order by confidence
            };

            _logger.LogInformation("Analysis successful. Found {Count} predictions.", analysisResult.Predictions.Count);
            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image URL {ImageUrl} with Custom Vision.", imageUrl);
            // Depending on requirements, we could return null, or throw, but right now we return an AnalysisResult with an error state
            return new AnalysisResult { ErrorMessage = "Failed to analyze image.", Predictions = new List<MedicalImageAI.Api.Models.PredictionModel>() };
        }
    }
}
