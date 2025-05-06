using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace MedicalImageAI.Api.Services;

/// <summary>
/// `BlobStorageService` is responsible for uploading images to Azure Blob Storage and generating SAS URIs.
/// It uses the Azure.Storage.Blobs library to interact with Azure Blob Storage.
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly string _containerName;

    public BlobStorageService(
        BlobServiceClient blobServiceClient, // Injected from Program.cs
        IConfiguration configuration,
        ILogger<BlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _configuration = configuration;
        _logger = logger;

        _containerName = configuration["BlobStorage:ContainerName"] ?? "uploaded-images"; // Default if not configured
        if (string.IsNullOrEmpty(_containerName)) {
            throw new InvalidOperationException("Blob container name is not configured (BlobStorage:ContainerName).");
        }
    }

    /// <summary>
    /// Uploads an image stream to Azure Blob Storage.
    /// Generates a unique blob name using a GUID and the original file extension.
    /// </summary>
    /// <param name="imageStream"></param>
    /// <param name="fileName"></param>
    /// <param name="contentType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<(string uniqueBlobName, string blobUri)> UploadImageAsync(Stream imageStream, string fileName, string contentType)
    {
        if (imageStream == null || imageStream.Length == 0)
        {
            throw new ArgumentException("Image stream cannot be null or empty.", nameof(imageStream));
        }
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
        }

        _logger.LogInformation("Attempting to upload file {FileName} to container {ContainerName}", fileName, _containerName);

        try
        {
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var uniqueBlobName = $"{Guid.NewGuid()}{ext}";

            BlobClient blobClient = containerClient.GetBlobClient(uniqueBlobName);

            await blobClient.UploadAsync(imageStream, new BlobHttpHeaders { ContentType = contentType });

            _logger.LogInformation("Successfully uploaded {FileName} as {BlobName}. Base URI: {BlobUri}", fileName, uniqueBlobName, blobClient.Uri);
            return (uniqueBlobName, blobClient.Uri.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to blob storage container {ContainerName}.", fileName, _containerName);
            throw; // Re-throw the exception to be handled by the caller (controller)
        }
    }

    /// <summary>
    /// Generates a temporary SAS URI with Read permissions for a specific blob.
    /// The URI is valid for 10 minutes and can be used to read the blob without authentication.
    /// The blob must exist in the specified container.
    /// If the blob does not exist, an empty string is returned.
    /// If the blob exists but SAS URI generation fails, an empty string is returned.
    /// If the blob exists and SAS URI generation is successful, the URI is returned.
    /// </summary>
    /// <param name="blobName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<string> GenerateReadSasUriAsync(string blobName)
    {
        if (string.IsNullOrEmpty(blobName))
        {
            throw new ArgumentException("Blob name cannot be null or empty.", nameof(blobName));
        }

        _logger.LogInformation("Generating Read SAS URI for blob {BlobName} in container {ContainerName}", blobName, _containerName);

        try
        {
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogError("Blob {BlobName} does not exist in container {ContainerName}. Cannot generate SAS URI.", blobName, _containerName);
                return ""; // Indicates failure to the caller
            }

            if (blobClient.CanGenerateSasUri)
            {
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = _containerName,
                    BlobName = blobName,
                    Resource = "b",
                    StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Start 5 mins ago for clock skew
                    ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(10), // Valid for 10 minutes
                    Protocol = SasProtocol.Https
                };
                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
                _logger.LogInformation("Successfully generated SAS URI for {BlobName} (valid for 10 mins).", blobName);
                return sasUri.ToString();
            }
            else
            {
                _logger.LogError("Cannot generate SAS URI for blob {BlobName}. Check storage account credentials/permissions.", blobName);
                return ""; // Indicates failure to the caller
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SAS URI for blob {BlobName}.", blobName);
            return ""; // Indicates failure to the caller
            // Or re-throw: throw;
        }
    }
}
