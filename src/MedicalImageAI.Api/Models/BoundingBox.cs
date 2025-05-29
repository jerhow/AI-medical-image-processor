namespace MedicalImageAI.Api.Models;

public class BoundingBox
{
    public double Left { get; set; }  // Typically 0.0 - 1.0 (relative to image width)
    public double Top { get; set; }   // Typically 0.0 - 1.0 (relative to image height)
    public double Width { get; set; } // Typically 0.0 - 1.0 (relative to image width)
    public double Height { get; set; }// Typically 0.0 - 1.0 (relative to image height)
}
