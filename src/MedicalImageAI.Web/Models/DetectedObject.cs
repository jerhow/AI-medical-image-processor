namespace MedicalImageAI.Web.Models;

public class DetectedObject
{
    public string? TagName { get; set; }
    public double Confidence { get; set; }
    public BoundingBox? BoundingBox { get; set; }
}
