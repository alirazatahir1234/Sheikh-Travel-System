namespace SheikhTravelSystem.Infrastructure.Services.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Local or Azure</summary>
    public string Provider { get; set; } = "Azure";

    public string RootPath { get; set; } = "uploads";
    public string PublicBasePath { get; set; } = "/uploads";
    /// <summary>Optional API origin for absolute file URLs, e.g. http://127.0.0.1:5082</summary>
    public string? PublicOrigin { get; set; }

    public string? AzureConnectionString { get; set; }
    public string AzureContainerName { get; set; } = "vehicle-files";
    /// <summary>Optional CDN or custom base URL. When empty, SAS URLs are generated for private containers.</summary>
    /// <remarks>
    /// Must never point at the playback container. Website marketing media may use public containers
    /// (website-images / website-videos); GPS playback coordinates must stay private + SAS only.
    /// </remarks>
    public string? AzurePublicBaseUrl { get; set; }
    /// <summary>Read SAS lifetime in days for general private blob containers (default 365).</summary>
    public int AzureSasExpiryDays { get; set; } = 365;

    /// <summary>
    /// Dedicated private container for GPS playback / history coordinate files.
    /// ACL must remain off (no anonymous public access). Default: gps-playbacks.
    /// </summary>
    public string PlaybackContainerName { get; set; } = "gps-playbacks";

    /// <summary>
    /// Read SAS lifetime in minutes for playback coordinate blobs (default 60).
    /// Kept short because payloads contain vehicle location history.
    /// </summary>
    public int PlaybackSasExpiryMinutes { get; set; } = 60;
}
