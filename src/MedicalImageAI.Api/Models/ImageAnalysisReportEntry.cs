namespace MedicalImageAI.Api.Models;

public class ImageAnalysisReportEntry
{
    public Guid JobId { get; set; }
    public required string OriginalFileName { get; set; }
    public DateTime UploadTimestamp { get; set; }
    public required string Status { get; set; } // e.g., "Completed", "Failed"
    public DateTime? ProcessingStartedTimestamp { get; set; }
    public DateTime? CompletedTimestamp { get; set; }
    public string? TopPredictionTag { get; set; } // e.g., "Cardiomegaly"
    public double TopPredictionProbability { get; set; } // e.g., 95.3
    public string? AllPredictionsSummary { get; set; } // Optional: a string summary of all predictions
    public string? ErrorMessage { get; set; } // If status is "Failed"
    public string? BlobUri { get; set; }
    public string? ExtractedOcrText { get; set; }
}
