namespace SheikhTravelSystem.Application.Features.GpsTracking.Trackers;

public static class TrackerCatalog
{
    public static readonly IReadOnlyDictionary<string, TrackerModelInfo> Models =
        new Dictionary<string, TrackerModelInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["teltonika_fmb920"] = new("Teltonika FMB920", "Teltonika", "teltonika", true),
            ["teltonika_fmb140"] = new("Teltonika FMB140", "Teltonika", "teltonika", true),
            ["teltonika_fmb001"] = new("Teltonika FMB001", "Teltonika", "teltonika", false),
            ["teltonika_fmc001"] = new("Teltonika FMC001", "Teltonika", "teltonika", false),
            ["concox_gt06n"]     = new("Concox GT06N", "Concox", "gt06", true),
            ["queclink_gv75"]    = new("Queclink GV75", "Queclink", "gl200", true),
        };

    public static TrackerModelInfo? Resolve(string? key)
        => key is not null && Models.TryGetValue(key, out var m) ? m : null;

    public static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "truck", "car", "bus", "motorcycle", "trailer", "asset"
    };

    public static readonly HashSet<string> ValidCurrentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Available", "Installed", "InStock", "Maintenance", "Damaged", "Removed"
    };

    public static readonly HashSet<string> InstallableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Available", "InStock", "Maintenance", "Removed", "Installed"
    };
}

public record TrackerModelInfo(string Label, string Vendor, string Protocol, bool SupportsEngineCutoff);

public record RegisterTrackerDto(
    string Name,
    string UniqueId,
    string Category,
    int TrackerModelId,
    string? TrackerModelKey = null,
    string? Phone = null,
    string? Contact = null,
    bool Disabled = false,
    int? VehicleId = null,
    int? DriverId = null,
    bool SupportsEngineCutoff = false,
    string? RelayOutput = null,
    string? RelayPurpose = null,
    DateTime? InstallationDate = null,
    string? InstalledBy = null,
    string? InstallationNotes = null,
    string? SerialNumber = null,
    string? CountryCode = null,
    string? SIMProvider = null,
    string? SIMPackage = null,
    decimal? MonthlySIMCost = null,
    DateOnly? WarrantyStart = null,
    DateOnly? WarrantyEnd = null,
    DateOnly? PurchaseDate = null,
    decimal? PurchasePrice = null,
    string? Vendor = null,
    string? CurrentStatus = "Available");

public record UpdateTrackerDto(
    string Name,
    string Category,
    int TrackerModelId,
    string? TrackerModelKey = null,
    string? Phone = null,
    string? Contact = null,
    bool Disabled = false,
    int? VehicleId = null,
    int? DriverId = null,
    bool SupportsEngineCutoff = false,
    string? RelayOutput = null,
    string? RelayPurpose = null,
    DateTime? InstallationDate = null,
    string? InstalledBy = null,
    string? InstallationNotes = null,
    string? SerialNumber = null,
    string? CountryCode = null,
    string? SIMProvider = null,
    string? SIMPackage = null,
    decimal? MonthlySIMCost = null,
    DateOnly? WarrantyStart = null,
    DateOnly? WarrantyEnd = null,
    DateOnly? PurchaseDate = null,
    decimal? PurchasePrice = null,
    string? Vendor = null,
    string? CurrentStatus = null,
    bool IsActive = true);

public record InstallTrackerDto(
    int VehicleId,
    int? DriverId = null,
    DateTime? InstallationDate = null,
    string? InstalledBy = null,
    string? InstallationNotes = null,
    string? RelayOutput = null);

public record TrackerRegisteredDto(
    int Id,
    string Name,
    string UniqueId,
    string ProtocolLabel,
    string? VehicleName,
    string? PlateNumber,
    int? TraccarDeviceId,
    string StatusMessage);

public record TrackerDetailDto(
    int Id,
    int? VehicleId,
    string? VehicleName,
    string? PlateNumber,
    int? DriverId,
    string? DriverName,
    string UniqueId,
    string Name,
    string? Category,
    string? Phone,
    string? Contact,
    bool Disabled,
    string? Protocol,
    string? TrackerModelKey,
    int? TrackerModelId,
    int? TrackerBrandId,
    string? TrackerBrandName,
    string? ModelName,
    string? Model,
    string? Vendor,
    bool SupportsEngineCutoff,
    string? RelayOutput,
    string? RelayPurpose,
    bool? LastIgnition,
    DateTime? LastSeenAt,
    bool IsActive,
    bool IsOnline,
    decimal? LastSpeed,
    decimal? LastBatteryLevel,
    int? LastRssi,
    int? TraccarDeviceId,
    bool IsTraccarLinked,
    bool IsValidImei,
    string? SerialNumber,
    DateTime? InstallationDate,
    string? InstalledBy,
    string? InstallationNotes,
    string? CountryCode,
    string? SIMProvider,
    string? SIMPackage,
    decimal? MonthlySIMCost,
    DateTime? WarrantyStart,
    DateTime? WarrantyEnd,
    DateTime? PurchaseDate,
    decimal? PurchasePrice,
    string? CurrentStatus,
    DateTime? LastSyncAt,
    string? SimNumber = null);
