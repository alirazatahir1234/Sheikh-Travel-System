using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Storage;

public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly FileStorageOptions _options;

    public AzureBlobStorageService(BlobServiceClient blobServiceClient, IOptions<FileStorageOptions> options)
    {
        _blobServiceClient = blobServiceClient;
        _options = options.Value;
    }

    public async Task<StoredFileResult> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var blobName = BuildBlobName(folder, fileName);

        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.AzureContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);
        var headers = new BlobHttpHeaders { ContentType = contentType };

        if (content.CanSeek)
            content.Position = 0;

        await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var size = properties.Value.ContentLength;
        var readUrl = BuildReadableUrl(blobClient, blobName);

        return new StoredFileResult(blobName, readUrl, Path.GetFileName(blobName), size, contentType);
    }

    public async Task DeleteAsync(string storageKeyOrLegacyUrl, CancellationToken cancellationToken = default)
    {
        var blobName = ResolveBlobName(storageKeyOrLegacyUrl);
        if (string.IsNullOrWhiteSpace(blobName))
            return;

        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.AzureContainerName);
        await containerClient.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public string ResolveReadUrl(string? storageKeyOrLegacyUrl)
    {
        if (string.IsNullOrWhiteSpace(storageKeyOrLegacyUrl))
            return string.Empty;

        if (IsAbsoluteUrl(storageKeyOrLegacyUrl))
            return storageKeyOrLegacyUrl;

        var blobName = ResolveBlobName(storageKeyOrLegacyUrl);
        if (string.IsNullOrWhiteSpace(blobName))
            return storageKeyOrLegacyUrl;

        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.AzureContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        return BuildReadableUrl(blobClient, blobName);
    }

    private string BuildBlobName(string folder, string fileName)
    {
        var safeFolder = string.Join('/', folder
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => string.Join('_', part.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))));

        var ext = Path.GetExtension(fileName);
        var storedName = $"{Guid.NewGuid():N}{ext}".ToLowerInvariant();
        return string.IsNullOrWhiteSpace(safeFolder) ? storedName : $"{safeFolder}/{storedName}";
    }

    private string BuildReadableUrl(BlobClient blobClient, string blobName)
    {
        if (!string.IsNullOrWhiteSpace(_options.AzurePublicBaseUrl))
            return $"{_options.AzurePublicBaseUrl.TrimEnd('/')}/{blobName}";

        if (blobClient.CanGenerateSasUri)
        {
            var sas = new BlobSasBuilder
            {
                BlobContainerName = _options.AzureContainerName,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(Math.Max(1, _options.AzureSasExpiryDays))
            };
            sas.SetPermissions(BlobSasPermissions.Read);
            return blobClient.GenerateSasUri(sas).ToString();
        }

        return blobClient.Uri.ToString();
    }

    private string? ResolveBlobName(string storageKeyOrLegacyUrl)
    {
        var path = storageKeyOrLegacyUrl.Trim();
        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
            path = path[..queryIndex];

        if (IsAbsoluteUrl(path))
        {
            if (!string.IsNullOrWhiteSpace(_options.AzurePublicBaseUrl))
            {
                var baseUrl = _options.AzurePublicBaseUrl.TrimEnd('/') + '/';
                return path.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase)
                    ? path[baseUrl.Length..]
                    : null;
            }

            var containerPrefix = $"/{_options.AzureContainerName}/";
            var idx = path.IndexOf(containerPrefix, StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? path[(idx + containerPrefix.Length)..] : null;
        }

        return path.TrimStart('/');
    }

    private static bool IsAbsoluteUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
