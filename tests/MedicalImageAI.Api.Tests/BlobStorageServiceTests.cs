using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MedicalImageAI.Api.Services;
using Moq;

namespace MedicalImageAI.Api.Tests;

public class BlobStorageServiceTests
{
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<BlobContainerClient> _mockBlobContainerClient;
    private readonly Mock<BlobClient> _mockBlobClient;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<BlobStorageService>> _mockLogger;
    private readonly BlobStorageService _blobStorageService;

    private const string TestContainerName = "test-images";
    private const string TestFileName = "testimage.png";
    private const string TestContentType = "image/png";

    public BlobStorageServiceTests()
    {
        // Mock IConfiguration to return the test container name
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(config => config["BlobStorage:ContainerName"])
                            .Returns(TestContainerName);

        // Mock ILogger
        _mockLogger = new Mock<ILogger<BlobStorageService>>();

        // Mock Azure Blob Storage clients
        _mockBlobClient = new Mock<BlobClient>();
        _mockBlobContainerClient = new Mock<BlobContainerClient>();
        _mockBlobServiceClient = new Mock<BlobServiceClient>();

        // Setup the chain of mock client creations:
        // 1. BlobServiceClient.GetBlobContainerClient should return our mock container client
        _mockBlobServiceClient.Setup(serviceClient => serviceClient.GetBlobContainerClient(TestContainerName))
                                .Returns(_mockBlobContainerClient.Object);

        // 2. BlobContainerClient.GetBlobClient should return our mock blob client
        // We use It.IsAny<string>() for the blobName because it's generated dynamically
        _mockBlobContainerClient.Setup(containerClient => containerClient.GetBlobClient(It.IsAny<string>()))
                                .Returns(_mockBlobClient.Object);
        
        // 3. Mock CreateIfNotExistsAsync on the container client to return a success-like response
        //    (Response<BlobContainerInfo> can be null for this purpose if the result isn't used)
        var mockContainerResponse = new Mock<Response<BlobContainerInfo>>();
        _mockBlobContainerClient.Setup(c => c.CreateIfNotExistsAsync(PublicAccessType.None, null, null, It.IsAny<CancellationToken>()))
                                .ReturnsAsync(mockContainerResponse.Object);


        // Instantiate the service we are testing, with its mocked dependencies
        _blobStorageService = new BlobStorageService(
            _mockBlobServiceClient.Object,
            _mockConfiguration.Object,
            _mockLogger.Object
        );
    }

    /// <summary>
    /// Test to ensure that UploadImageAsync method correctly uploads an image to Azure Blob Storage.
    /// - Verifies that the blob name is unique and follows the expected format.
    /// - Checks that the blob URI is correctly formed.
    /// - The test uses a mock stream to simulate the image upload process.
    /// - Verifies that the correct methods are called on the mocked BlobServiceClient and BlobContainerClient.
    /// - Also checks that the correct content type is set in the BlobHttpHeaders.
    /// - Ensures that the blob name is a valid GUID and that the file extension matches the original file name.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task UploadImageAsync_ValidInput_ShouldUploadAndReturnCorrectInfo()
    {
        // --- Arrange ---
        var testImageStream = new MemoryStream(new byte[] { 1, 2, 3 }); // Dummy stream
        string? capturedBlobName = null; // To capture the generated blob name

        // Refine GetBlobClient setup to capture the generated name
        _mockBlobContainerClient.Setup(containerClient => containerClient.GetBlobClient(It.IsAny<string>()))
                                .Callback<string>(name => capturedBlobName = name) // Capture the name
                                .Returns(_mockBlobClient.Object);

        // Setup mockBlobClient.Uri to return a predictable URI based on the captured name
        // This needs to be done carefully as capturedBlobName is set during the GetBlobClient call.
        // We'll set it up to return a URI that incorporates the TestContainerName and whatever blobName it gets.
        // This setup will be used when blobClient.Uri is accessed AFTER GetBlobClient is called.
        _mockBlobClient.SetupGet(client => client.Uri)
                        .Returns(() => new Uri($"https://fakestorage.blob.core.windows.net/{TestContainerName}/{capturedBlobName}"));
        // Using a lambda allows it to use the capturedBlobName at the time Uri is accessed

        // Setup UploadAsync to return a successful-like response
        // Create a dummy BlobContentInfo
        var dummyBlobContentInfo = BlobsModelFactory.BlobContentInfo(
            eTag: new ETag("ETAG_VALUE"),
            lastModified: DateTimeOffset.UtcNow,
            contentHash: Array.Empty<byte>(),
            versionId: null,
            encryptionKeySha256: null,
            encryptionScope: null,
            blobSequenceNumber: 0L   // Explicitly 0L for a long type
        );

        // (dummyBlobContentInfo is already created correctly from the previous step)
        // var dummyBlobContentInfo = BlobsModelFactory.BlobContentInfo(...);

        // 1. Create a mock for the underlying raw Azure.Response
        var mockRawHttpResponse = new Mock<Azure.Response>();
        // Optionally, set up its Status property if your code directly or indirectly checks it.
        // For a successful blob upload, status codes like 201 (Created) are common.
        mockRawHttpResponse.SetupGet(r => r.Status).Returns(201); // Example status

        // 2. Create the actual Response<BlobContentInfo> using Azure.Response.FromValue
        // This wraps your dummyBlobContentInfo with the mocked raw HTTP response.
        Azure.Response<BlobContentInfo> actualAzureResponse = Azure.Response.FromValue(dummyBlobContentInfo, mockRawHttpResponse.Object);

        // 3. Setup the _mockBlobClient.UploadAsync method to return this actualAzureResponse
        _mockBlobClient.Setup(client => client.UploadAsync(
            It.IsAny<Stream>(),                          // content (matches the 'stream')
            It.IsAny<BlobHttpHeaders>(),                 // httpHeaders (matches your 'new BlobHttpHeaders { ... }')
            It.IsAny<IDictionary<string, string>>(),     // metadata (will match the default 'null')
            It.IsAny<BlobRequestConditions>(),           // conditions (will match the default 'null')
            It.IsAny<IProgress<long>>(),                 // progressHandler (will match the default 'null')
            It.IsAny<AccessTier?>(),                     // accessTier (will match the default 'null')
            It.IsAny<StorageTransferOptions>(),          // transferOptions (will match the default 'default(StorageTransferOptions)')
            It.IsAny<CancellationToken>()                // cancellationToken (will match the default 'default(CancellationToken)')
        ))
        .ReturnsAsync(actualAzureResponse); // 'actualAzureResponse' is your correctly created Azure.Response<BlobContentInfo>


        // --- Act ---
        var (uniqueBlobName, blobUri) = await _blobStorageService.UploadImageAsync(testImageStream, TestFileName, TestContentType);

        // --- Assert ---
        // --- Verify GetBlobContainerClient was called with the correct container name
        _mockBlobServiceClient.Verify(client => client.GetBlobContainerClient(TestContainerName), Times.Once);

        // --- Verify CreateIfNotExistsAsync was called on the container
        _mockBlobContainerClient.Verify(client => client.CreateIfNotExistsAsync(PublicAccessType.None, null, null, It.IsAny<CancellationToken>()), Times.Once);

        // --- Verify GetBlobClient was called on the container (capturedBlobName should now be set)
        _mockBlobContainerClient.Verify(client => client.GetBlobClient(
            It.Is<string>(s =>
                // Check if the string ends with the expected extension
                s.EndsWith(Path.GetExtension(TestFileName)) &&
                // Check if the filename part (without extension) has the length of a standard GUID string
                Path.GetFileNameWithoutExtension(s).Length == 36 &&
                // Optionally, check for the presence of hyphens typical in GUIDs
                Path.GetFileNameWithoutExtension(s).Count(c => c == '-') == 4
            )),
            Times.Once);

        Assert.NotNull(capturedBlobName); // Ensure it was captured
        Assert.EndsWith($".{Path.GetExtension(TestFileName).TrimStart('.')}", uniqueBlobName); // Check extension
        Assert.Equal(uniqueBlobName, capturedBlobName); // uniqueBlobName from result should match captured one

        // --- Verify UploadAsync was called on the blob client
        _mockBlobClient.Verify(client => client.UploadAsync(
            testImageStream,                                               // 1. Verify it's the same stream instance
            It.Is<BlobHttpHeaders>(h => h.ContentType == TestContentType), // 2. Verify ContentType in headers
            It.IsAny<IDictionary<string, string>>(),                       // 3. metadata (matches default null)
            It.IsAny<BlobRequestConditions>(),                             // 4. conditions (matches default null)
            It.IsAny<IProgress<long>>(),                                   // 5. progressHandler (matches default null)
            It.IsAny<AccessTier?>(),                                       // 6. accessTier (matches default null)
            It.IsAny<StorageTransferOptions>(),                            // 7. transferOptions (matches default struct)
            It.IsAny<CancellationToken>()                                  // 8. cancellationToken (matches default struct)
        ), Times.Once);

        // --- Assert the returned values
        Assert.NotNull(uniqueBlobName);
        Assert.EndsWith($".{Path.GetExtension(TestFileName).TrimStart('.')}", uniqueBlobName);
        Assert.True(Guid.TryParse(Path.GetFileNameWithoutExtension(uniqueBlobName), out _)); // Check GUID part

        Assert.NotNull(blobUri);
        Assert.Equal($"https://fakestorage.blob.core.windows.net/{TestContainerName}/{uniqueBlobName}", blobUri);
    }   
}
