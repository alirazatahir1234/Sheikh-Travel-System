namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Private blob storage for GPS playback / history coordinate files (GPX, GeoJSON, KML, CSV, JSON).
/// Always served via time-limited signed URLs — never anonymous public blob access.
/// Website marketing media must use separate containers (e.g. website-images) and must not share this path.
/// </summary>
public interface IPlaybackCoordinateStorage
{
    /// <summary>Container (or local root segment) used for playback coordinate blobs.</summary>
    string ContainerName { get; }

    /// <summary>
    /// Stores a playback coordinate file under a private container and returns a short-lived read SAS URL.
    /// </summary>
    Task<PlaybackStoredFileResult> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default);

    /// <summary>Issues a fresh time-limited read SAS URL for an existing playback storage key.</summary>
    string ResolveSignedReadUrl(string storageKey);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public record PlaybackStoredFileResult(
    string StorageKey,
    string SignedUrl,
    DateTimeOffset ExpiresOn,
    string FileName,
    long SizeBytes,
    string ContentType);
