namespace MedicalImageAI.Api.Models;

public class UploadResponse
{
    public string? FileName { get; set; }
    public string? BlobUri { get; set; }
    public string? Message { get; set; }
    public AnalysisResult? Analysis { get; set; } = null;
}
