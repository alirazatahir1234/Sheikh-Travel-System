using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Storage;

public class AzureBlobStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;

    public AzureBlobStorageService(IOptions<FileStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<StoredFileResult> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AzureConnectionString))
        {
            throw new InvalidOperationException(
                "Azure Blob Storage is not configured. Set FileStorage:AzureConnectionString.");
        }

        var safeFolder = string.Join('/', folder
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => string.Join('_', part.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))));

        var ext = Path.GetExtension(fileName);
        var storedName = $"{Guid.NewGuid():N}{ext}".ToLowerInvariant();
        var blobName = string.IsNullOrWhiteSpace(safeFolder)
            ? storedName
            : $"{safeFolder}/{storedName}";

        var blobServiceClient = new BlobServiceClient(_options.AzureConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_options.AzureContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);
        var headers = new BlobHttpHeaders { ContentType = contentType };

        if (content.CanSeek)
            content.Position = 0;

        var upload = await blobClient.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = headers
        }, cancellationToken);

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var size = properties.Value.ContentLength;
        var publicUrl = !string.IsNullOrWhiteSpace(_options.AzurePublicBaseUrl)
            ? $"{_options.AzurePublicBaseUrl.TrimEnd('/')}/{blobName}"
            : blobClient.Uri.ToString();

        return new StoredFileResult(publicUrl, storedName, size, contentType);
    }
}
