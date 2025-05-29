namespace MedicalImageAI.Web.Models;

public class AnalysisStatusResponse // The top-level object from the API
{
    public Guid JobId { get; set; }
    public string? Status { get; set; }
    public DateTime UploadTimestamp { get; set; }
    public DateTime? ProcessingStartedTimestamp { get; set; }
    public DateTime? CompletedTimestamp { get; set; }
    public AnalysisResult? Analysis { get; set; }
    public string? OriginalFileName { get; set; }
    public string? BlobUri { get; set; }
}
