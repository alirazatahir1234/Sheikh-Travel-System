using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Storage;

/// <summary>
/// Stores GPS playback coordinate files in a dedicated private Azure container and serves them
/// exclusively via short-lived blob SAS URLs. Never uses <see cref="FileStorageOptions.AzurePublicBaseUrl"/>.
/// </summary>
public sealed class AzurePlaybackCoordinateStorage : IPlaybackCoordinateStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly FileStorageOptions _options;

    public AzurePlaybackCoordinateStorage(BlobServiceClient blobServiceClient, IOptions<FileStorageOptions> options)
    {
        _blobServiceClient = blobServiceClient;
        _options = options.Value;
    }

    public string ContainerName =>
        string.IsNullOrWhiteSpace(_options.PlaybackContainerName)
            ? "gps-playbacks"
            : _options.PlaybackContainerName.Trim();

    public async Task<PlaybackStoredFileResult> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var blobName = BuildBlobName(folder, fileName);
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);

        // Always private — website public containers must never be reused here.
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);
        var headers = new BlobHttpHeaders { ContentType = contentType };

        if (content.CanSeek)
            content.Position = 0;

        await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var (signedUrl, expiresOn) = BuildSignedReadUrl(blobClient, blobName);

        return new PlaybackStoredFileResult(
            blobName,
            signedUrl,
            expiresOn,
            Path.GetFileName(blobName),
            properties.Value.ContentLength,
            contentType);
    }

    public string ResolveSignedReadUrl(string storageKey)
    {
        var blobName = ResolveBlobName(storageKey);
        if (string.IsNullOrWhiteSpace(blobName))
            return string.Empty;

        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        var (url, _) = BuildSignedReadUrl(blobClient, blobName);
        return url;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var blobName = ResolveBlobName(storageKey);
        if (string.IsNullOrWhiteSpace(blobName))
            return;

        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private (string Url, DateTimeOffset ExpiresOn) BuildSignedReadUrl(BlobClient blobClient, string blobName)
    {
        // Intentional: never fall back to AzurePublicBaseUrl / CDN for playback coordinates.
        if (!blobClient.CanGenerateSasUri)
            throw new InvalidOperationException(
                "Playback coordinate blobs require a credential that can generate SAS URIs. " +
                "Public/anonymous blob URLs are not allowed for gps-playbacks.");

        var expiresOn = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_options.PlaybackSasExpiryMinutes, 5, 24 * 60));
        var sas = new BlobSasBuilder
        {
            BlobContainerName = ContainerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = expiresOn
        };
        sas.SetPermissions(BlobSasPermissions.Read);
        return (blobClient.GenerateSasUri(sas).ToString(), expiresOn);
    }

    private static string BuildBlobName(string folder, string fileName)
    {
        var safeFolder = string.Join('/', folder
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => string.Join('_', part.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))));

        var ext = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var safeBase = string.Join('_', (baseName ?? "playback")
            .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeBase))
            safeBase = "playback";

        var storedName = $"{safeBase}-{Guid.NewGuid():N}{ext}".ToLowerInvariant();
        return string.IsNullOrWhiteSpace(safeFolder) ? storedName : $"{safeFolder}/{storedName}";
    }

    private string? ResolveBlobName(string storageKeyOrLegacyUrl)
    {
        var path = storageKeyOrLegacyUrl.Trim();
        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
            path = path[..queryIndex];

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var containerPrefix = $"/{ContainerName}/";
            var idx = path.IndexOf(containerPrefix, StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? path[(idx + containerPrefix.Length)..] : null;
        }

        return path.TrimStart('/');
    }
}
