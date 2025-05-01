namespace MedicalImageAI.Api.Models;

public class AnalysisResult
{
    public DateTime Timestamp { get; set; }
    public List<PredictionModel> Predictions { get; set; } = new List<PredictionModel>();
    public string? ErrorMessage { get; set; } = null;
    public bool Success => ErrorMessage == null;
}
