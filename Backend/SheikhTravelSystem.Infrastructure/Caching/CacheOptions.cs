namespace SheikhTravelSystem.Infrastructure.Caching;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>When set, distributed Redis cache is used in addition to memory.</summary>
    public string? RedisConnectionString { get; set; }

    public int DashboardTtlSeconds { get; set; } = 60;

    public int SettingsTtlSeconds { get; set; } = 60;

    public int TrackerCatalogTtlSeconds { get; set; } = 300;
}
