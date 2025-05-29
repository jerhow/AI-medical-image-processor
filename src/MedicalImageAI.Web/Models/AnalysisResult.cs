namespace MedicalImageAI.Web.Models;

public class AnalysisResult
{
    public DateTime Timestamp { get; set; }
    public List<PredictionModel> Predictions { get; set; }
    public string? OcrText { get; set; } = null;
    public List<DetectedObject> DetectedObjects { get; set; }
    public string? ErrorMessage { get; set; } = null;
    public bool Success { get; set; }

    public AnalysisResult()
    {
        Predictions = new List<PredictionModel>();
        DetectedObjects = new List<DetectedObject>();
    }
}
