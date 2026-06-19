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
    public string? AzurePublicBaseUrl { get; set; }
    /// <summary>Read SAS lifetime in days for private blob containers (default 365).</summary>
    public int AzureSasExpiryDays { get; set; } = 365;
}
