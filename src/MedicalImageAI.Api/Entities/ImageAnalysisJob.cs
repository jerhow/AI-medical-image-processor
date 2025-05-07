using System.ComponentModel.DataAnnotations; // For [Key] attribute
using System.ComponentModel.DataAnnotations.Schema; // For [Column(TypeName = "...")]

namespace MedicalImageAI.Api.Entities;

public class ImageAnalysisJob
{
    [Key] // Primary key
    public Guid Id { get; set; } // Type: Guid

    [Required]
    public required string OriginalFileName { get; set; }

    [Required]
    public required string BlobUri { get; set; }

    public DateTime UploadTimestamp { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } // e.g., "Queued", "Processing", "Completed", "Failed"

    // Store the analysis result as a JSON string for simplicity
    // Could be normalized into separate tables for more complex querying, but JSON is fine for now
    [Column(TypeName = "nvarchar(max)")] // Ensure enough space for JSON
    public string? AnalysisResultJson { get; set; }

    public DateTime? ProcessingStartedTimestamp { get; set; }
    public DateTime? CompletedTimestamp { get; set; }

    public ImageAnalysisJob()
    {
        Id = Guid.NewGuid(); // Generate ID on creation
        UploadTimestamp = DateTime.UtcNow;
        Status = "Queued"; // Default status
    }
}
