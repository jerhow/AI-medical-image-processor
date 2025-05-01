namespace MedicalImageAI.Api.Models;

public class PredictionModel
{
    public string? TagName { get; set; }
    public double Probability { get; set; } // As percentage e.g., 95.3%
}
