namespace MedicalImageAI.Api.Models;

public class DetectedObject
{
    public string? TagName { get; set; }
    public double Confidence { get; set; } // Probability, e.g., 0.0 - 1.0 (sticking with fractions for this, but one could also use 0-100 for percentage representation)
    public BoundingBox? BoundingBox { get; set; }
}
