using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;
using MedicalImageAI.Api.Models;

namespace MedicalImageAI.Api.Services;

public class CustomVisionService : ICustomVisionService
{
    private readonly ILogger<CustomVisionService> _logger;
    private readonly CustomVisionPredictionClient _predictionClient;
    private readonly Guid _customVisionProjectId;
    private readonly string _publishedModelName;
    private readonly Guid _objectDetectionProjectId;
    private readonly string _objectDetectionPublishedModelName;
    private readonly double _objectDetectionConfidenceThreshold;

    public CustomVisionService(IConfiguration configuration, ILogger<CustomVisionService> logger)
    {
        _logger = logger;

        string predictionKey = configuration["CustomVision:PredictionKey"] ?? throw new ArgumentNullException("CustomVision:PredictionKey not configured");
        string endpoint = configuration["CustomVision:PredictionEndpoint"] ?? throw new ArgumentNullException("CustomVision:PredictionEndpoint not configured");
        string projectIdString = configuration["CustomVision:ProjectId"] ?? throw new ArgumentNullException("CustomVision:ProjectId not configured");
        _publishedModelName = configuration["CustomVision:PublishedModelName"] ?? throw new ArgumentNullException("CustomVision:PublishedModelName not configured");

        if (!Guid.TryParse(projectIdString, out _customVisionProjectId))
        {
            throw new ArgumentException("CustomVision:ProjectId is not a valid GUID.");
        }

        // For object detection
        _objectDetectionPublishedModelName = configuration["CustomVisionOD:PublishedModelName"] ?? throw new ArgumentNullException("CustomVisionOD:PublishedModelName not configured");
        _objectDetectionConfidenceThreshold = double.Parse(configuration["CustomVisionOD:ConfidenceThreshold"] ?? throw new ArgumentNullException("CustomVisionOD:ConfidenceThreshold not configured"));
        string odProjectIdString = configuration["CustomVisionOD:ProjectId"] ?? throw new ArgumentNullException("CustomVisionOD:ProjectId not configured");
        if (!Guid.TryParse(odProjectIdString, out _objectDetectionProjectId))
        {
            throw new ArgumentException("CustomVisionOD:ProjectId is not a valid GUID.");
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
            ImagePrediction predictionResult = await _predictionClient.ClassifyImageUrlAsync(_customVisionProjectId, _publishedModelName, url);

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

    /// <summary>
    /// Asynchronously detects objects in an image URL using the Custom Vision Object Detection model.
    /// This method uses the Custom Vision Prediction API to detect objects and return their bounding boxes and confidence scores.
    /// NOTE: This method is specifically for object detection, which is separate from image classification and uses a completely different model.
    /// </summary>
    /// <param name="imageUrl"></param>
    /// <returns></returns>
    public async Task<List<DetectedObject>> DetectObjectsAsync(string imageUrl)
    {
        _logger.LogInformation("Detecting objects for image URL: {ImageUrl} using OD model {ModelName}", imageUrl, _objectDetectionPublishedModelName);
        var detectedObjectsList = new List<DetectedObject>();

        try
        {
            ImageUrl url = new ImageUrl(imageUrl);
            ImagePrediction objectDetectionResult = await _predictionClient.DetectImageUrlAsync( // Use DetectImageUrlAsync for object detection
                _objectDetectionProjectId,
                _objectDetectionPublishedModelName,
                url
            );

            if (objectDetectionResult.Predictions != null)
            {
                _logger.LogInformation("Applying confidence threshold of {Threshold} to object detection results.", _objectDetectionConfidenceThreshold);

                foreach (var prediction in objectDetectionResult.Predictions)
                {
                    if (prediction.Probability > _objectDetectionConfidenceThreshold)
                    {
                        detectedObjectsList.Add(new DetectedObject
                        {
                            TagName = prediction.TagName,
                            Confidence = prediction.Probability * 100, // Convert to percentage
                            BoundingBox = new Models.BoundingBox // Ensure this refers to your BoundingBox model
                            {
                                Left = prediction.BoundingBox.Left,
                                Top = prediction.BoundingBox.Top,
                                Width = prediction.BoundingBox.Width,
                                Height = prediction.BoundingBox.Height
                            }
                        });
                    }
                }
                _logger.LogInformation("Object detection successful. Found {Count} objects.", detectedObjectsList.Count);
            }
            else
            {
                _logger.LogInformation("Object detection returned no predictions for {ImageUrl}.", imageUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting objects for image URL {ImageUrl} with Custom Vision OD model.", imageUrl);
            // Depending on requirements, this can return an empty list, null, or throw.
            // For now, returning an empty list on error.
        }
        return detectedObjectsList;
    }
}
