namespace SheikhTravelSystem.Infrastructure.Services.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Local or Azure</summary>
    public string Provider { get; set; } = "Azure";

    public string RootPath { get; set; } = "uploads";
    public string PublicBasePath { get; set; } = "/uploads";

    public string? AzureConnectionString { get; set; }
    public string AzureContainerName { get; set; } = "vehicle-files";
    /// <summary>Optional CDN or custom base URL. When empty, the blob's absolute URI is stored.</summary>
    public string? AzurePublicBaseUrl { get; set; }
}
