namespace MedicalImageAI.Api.Models;

public class AnalysisResult
{
    public DateTime Timestamp { get; set; }
    public List<PredictionModel> Predictions { get; set; }
    public string? OcrText { get; set; }
    public List<DetectedObject> DetectedObjects { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Success => ErrorMessage == null; // TODO: Re-evaluate this if OD or OCR can fail independently.
    // For now, assume that `Success` primarily reflects the classification step, and errors/empty results from OD/OCR are handled within their respective result properties.

    public AnalysisResult() // Initialize lists in constructor
    {
        Predictions = new List<PredictionModel>();
        DetectedObjects = new List<DetectedObject>();
    }
}
