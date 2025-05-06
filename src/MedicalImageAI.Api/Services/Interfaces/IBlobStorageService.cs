namespace MedicalImageAI.Api.Services;

public interface IBlobStorageService
{
    /// <summary>
    /// Uploads an image stream to Azure Blob Storage.
    /// </summary>
    /// <param name="imageStream">The stream containing the image data.</param>
    /// <param name="fileName">The original file name (used for extension).</param>
    /// <param name="contentType">The content type of the image.</param>
    /// <returns>A tuple containing the unique blob name and the full base URI of the uploaded blob.</returns>
    Task<(string uniqueBlobName, string blobUri)> UploadImageAsync(Stream imageStream, string fileName, string contentType);

    /// <summary>
    /// Generates a temporary SAS URI with Read permissions for a specific blob.
    /// </summary>
    /// <param name="blobName">The unique name of the blob.</param>
    /// <returns>The SAS URI string, or null/empty if generation fails.</returns>
    Task<string> GenerateReadSasUriAsync(string blobName);
}
