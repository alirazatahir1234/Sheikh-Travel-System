namespace SheikhTravelSystem.Application.Common;

/// <summary>Default cache TTLs for read-heavy endpoints.</summary>
public static class AppCacheTtl
{
    public static readonly TimeSpan Dashboard = TimeSpan.FromSeconds(60);
    /// <summary>Driver Management Operational / Risk KPI strip — short TTL; safe to lag briefly.</summary>
    public static readonly TimeSpan DriverStats = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Settings = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan SettingsCategories = TimeSpan.FromHours(1);
    public static readonly TimeSpan TrackerCatalog = TimeSpan.FromMinutes(5);
}
