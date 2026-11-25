using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Storage;

/// <summary>
/// Travel documents in a dedicated private Azure container. Never generates SAS or public URLs —
/// reads go through <see cref="OpenReadAsync"/> behind the authenticated download endpoint.
/// </summary>
public sealed class AzureTravelDocumentStorage(BlobServiceClient blobServiceClient, IOptions<TravelDocumentStorageOptions> options)
    : ITravelDocumentStorage
{
    private string ContainerName =>
        string.IsNullOrWhiteSpace(options.Value.AzureContainerName) ? "travel-documents" : options.Value.AzureContainerName.Trim();

    public async Task<string> SaveAsync(byte[] content, string contentType, string extension, int tenantId, int visaCaseId,
        CancellationToken cancellationToken = default)
    {
        var key = TravelDocumentStorageKeys.Build(tenantId, visaCaseId, extension);
        var container = blobServiceClient.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        using var stream = new MemoryStream(content, writable: false);
        await container.GetBlobClient(key).UploadAsync(stream,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, cancellationToken);
        return key;
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (!TravelDocumentStorageKeys.IsValid(storageKey))
            throw new InvalidOperationException("Invalid travel document key.");
        var blob = blobServiceClient.GetBlobContainerClient(ContainerName).GetBlobClient(storageKey);
        return await blob.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (!TravelDocumentStorageKeys.IsValid(storageKey)) return;
        await blobServiceClient.GetBlobContainerClient(ContainerName).GetBlobClient(storageKey)
            .DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
