namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<StoredFileResult> SaveAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKeyOrLegacyUrl, CancellationToken cancellationToken = default);
    string ResolveReadUrl(string? storageKeyOrLegacyUrl);
}

public record StoredFileResult(
    string StorageKey,
    string ReadUrl,
    string FileName,
    long SizeBytes,
    string ContentType);
