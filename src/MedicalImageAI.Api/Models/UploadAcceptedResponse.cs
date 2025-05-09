namespace MedicalImageAI.Api.Models;

public class UploadAcceptedResponse
{
    public required string Message { get; set; }
    public Guid JobId { get; set; }
    public required string FileName { get; set; }
    public required string BlobUri { get; set; }
}
