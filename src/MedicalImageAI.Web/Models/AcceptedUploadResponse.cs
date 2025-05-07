namespace MedicalImageAI.Web.Models;

/// <summary>
/// Represents the 202 Accepted response from the API after a successful image upload.
/// </summary>
public class AcceptedUploadResponse
{
    public string? Message { get; set; }
    public string? FileName { get; set; } // The unique name given by the server (e.g., GUID.png)
    public string? BlobUri { get; set; }  // The base URI of the blob in Azure Storage
    
    // Add ImageId or JobId later if the API starts returning it
}
