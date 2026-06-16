namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<StoredFileResult> SaveAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default);
}

public record StoredFileResult(string RelativeUrl, string FileName, long SizeBytes, string ContentType);
