using MedicalImageAI.Api.Models;

public class AnalysisStatusResponse
{
    public Guid JobId { get; set; }
    public required string Status { get; set; }
    public DateTime UploadTimestamp { get; set; }
    public DateTime? ProcessingStartedTimestamp { get; set; }
    public DateTime? CompletedTimestamp { get; set; }
    public required AnalysisResult Analysis { get; set; } // This will be populated if status is "Completed"
    public required string OriginalFileName { get; set; }
    public required string BlobUri { get; set; }
}
