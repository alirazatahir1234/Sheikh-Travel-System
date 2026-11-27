namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Who a geofence applies to. String values over the wire (TypeScript: GeofenceAppliesTo).</summary>
public static class GeofenceAppliesTo
{
    public const string All = "all";
    public const string Groups = "groups";
    public const string Vehicles = "vehicles";
    public const string Drivers = "drivers";

    public static readonly IReadOnlyList<string> Values = [All, Groups, Vehicles, Drivers];
}

/// <summary>A visit's status in the visits report (TypeScript: GeofenceVisitStatus). First match wins, in this order.</summary>
public static class GeofenceVisitStatus
{
    public const string Inside = "inside";
    public const string Overspeed = "overspeed";
    public const string Overstay = "overstay";
    public const string AfterHours = "after_hours";
    public const string Idle = "idle";
    public const string Completed = "completed";

    public static readonly IReadOnlyList<string> Values = [Inside, Overspeed, Overstay, AfterHours, Idle, Completed];
}

/// <summary>Kinds of items in a geofence's day activity (TypeScript: GeofenceActivityKind).</summary>
public static class GeofenceActivityKind
{
    public const string Entered = "entered";
    public const string Exited = "exited";
    public const string Dwell = "dwell";
    public const string Overspeed = "overspeed";
    public const string Idle = "idle";
    public const string AfterHoursExit = "after_hours_exit";
    public const string NotArrived = "not_arrived";
}

/// <summary>One row of the geofences list (left panel and map).</summary>
public sealed class GeofenceListItemDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string AreaType { get; init; } = "circle";
    public double CenterLat { get; init; }
    public double CenterLng { get; init; }
    public double RadiusMeters { get; init; }
    public string? GeoJson { get; init; }
    public bool IsActive { get; init; }
    public string Color { get; init; } = "#0f766e";
    public string? Type { get; init; }
    public bool IsRestricted { get; init; }
    public int? GroupId { get; init; }
    public string? GroupName { get; init; }
    public string? Address { get; init; }
    public string? Description { get; init; }
    public int BufferMeters { get; init; }
    public int? CorridorWidthMeters { get; init; }
    public double? AreaSquareMeters { get; init; }
    public double? PerimeterMeters { get; init; }
    public bool AppliesToAll { get; init; }
    public int VehiclesCovered { get; init; }
    public int DriversAssigned { get; init; }
    public int InsideNow { get; init; }
    public int EntriesToday { get; init; }
    public int ViolationsToday { get; init; }
    public int RuleCount { get; init; }
    public bool AlertOnEnter { get; init; }
    public bool AlertOnExit { get; init; }
}

/// <summary>Everything the editor and the detail panel show for one geofence.</summary>
public sealed class GeofenceDetailDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Type { get; init; }
    public bool IsRestricted { get; init; }
    public int? GroupId { get; init; }
    public string? GroupName { get; init; }
    public string? Address { get; init; }
    public string? Description { get; init; }
    public string Color { get; init; } = "#0f766e";
    public bool IsActive { get; init; }
    public string AreaType { get; init; } = "circle";
    public double CenterLat { get; init; }
    public double CenterLng { get; init; }
    public double RadiusMeters { get; init; }
    public string? GeoJson { get; init; }
    public int? CorridorWidthMeters { get; init; }
    public int BufferMeters { get; init; }
    public double? AreaSquareMeters { get; init; }
    public double? PerimeterMeters { get; init; }
    public bool AlertOnEnter { get; init; }
    public bool AlertOnExit { get; init; }
    public int? DwellMinutes { get; init; }
    public decimal? SpeedLimitKmh { get; init; }
    public int? IdleMinutes { get; init; }
    /// <summary>"HH:mm" in the company's time zone.</summary>
    public string? NotArrivedBy { get; init; }
    public bool EngineBlockOnExit { get; init; }
    /// <summary>Mon 1 … Sun 64; null = rules always active.</summary>
    public int? ScheduleDays { get; init; }
    public string? ScheduleStart { get; init; }
    public string? ScheduleEnd { get; init; }
    public bool AfterHoursExitCritical { get; init; }
    public IReadOnlyList<string> NotifyChannels { get; init; } = [];
    public int CooldownMinutes { get; init; }
    public string AppliesTo { get; init; } = GeofenceAppliesTo.Vehicles;
    public IReadOnlyList<int> VehicleIds { get; init; } = [];
    public IReadOnlyList<int> BranchIds { get; init; } = [];
    public IReadOnlyList<int> DepartmentIds { get; init; } = [];
    public IReadOnlyList<int> DriverIds { get; init; } = [];
    public IReadOnlyList<int> RecipientRoleIds { get; init; } = [];
    public IReadOnlyList<int> RecipientUserIds { get; init; } = [];
    public int VehiclesCovered { get; init; }
    public int RuleCount { get; init; }
}

/// <summary>Create or update payload: details, shape, rules, schedule, who it applies to and notify, in one step.</summary>
public sealed class SaveGeofenceDto
{
    public string Name { get; init; } = string.Empty;
    public string? Type { get; init; }
    public int? GroupId { get; init; }
    public string? Address { get; init; }
    public string? Description { get; init; }
    public string? Color { get; init; }
    public bool IsActive { get; init; } = true;
    public string AreaType { get; init; } = "circle";
    public double CenterLat { get; init; }
    public double CenterLng { get; init; }
    public double RadiusMeters { get; init; }
    public string? GeoJson { get; init; }
    public int? CorridorWidthMeters { get; init; }
    public int BufferMeters { get; init; } = 25;
    public bool AlertOnEnter { get; init; }
    public bool AlertOnExit { get; init; }
    public int? DwellMinutes { get; init; }
    public decimal? SpeedLimitKmh { get; init; }
    public int? IdleMinutes { get; init; }
    public string? NotArrivedBy { get; init; }
    public bool EngineBlockOnExit { get; init; }
    public int? ScheduleDays { get; init; }
    public string? ScheduleStart { get; init; }
    public string? ScheduleEnd { get; init; }
    public bool AfterHoursExitCritical { get; init; }
    public string AppliesTo { get; init; } = GeofenceAppliesTo.Vehicles;
    public int[]? VehicleIds { get; init; }
    public int[]? BranchIds { get; init; }
    public int[]? DepartmentIds { get; init; }
    public int[]? DriverIds { get; init; }
    public string[]? NotifyChannels { get; init; }
    public int[]? RecipientRoleIds { get; init; }
    public int[]? RecipientUserIds { get; init; }
    public int CooldownMinutes { get; init; } = 15;
}

/// <summary>Main screen KPI cards. Each value says its window in the name.</summary>
public sealed class GeofenceSummaryDto
{
    public int Total { get; init; }
    public int Active { get; init; }
    public int VehiclesInsideNow { get; init; }
    public int VehiclesAssigned { get; init; }
    public int EntriesToday { get; init; }
    public int ExitsToday { get; init; }
    public int ViolationsToday { get; init; }
    public int RestrictedEntriesToday { get; init; }
    public int OverstaysToday { get; init; }
    public int OverspeedsToday { get; init; }
    public int IdleAlertsToday { get; init; }
    public int AfterHoursExitsToday { get; init; }
    public int NotArrivedToday { get; init; }
    /// <summary>Average of completed visits that ended in the last 7 days; null when there were none.</summary>
    public double? AvgMinutesOnSiteLast7Days { get; init; }
}

public sealed class GeofenceInsideVehicleDto
{
    public long VisitId { get; init; }
    public int VehicleId { get; init; }
    public string VehicleLabel { get; init; } = string.Empty;
    public string? DriverName { get; init; }
    public DateTime EnteredUtc { get; init; }
    public int MinutesInside { get; init; }
    public bool OverDwell { get; init; }
}

public sealed class GeofenceActivityItemDto
{
    public DateTime AtUtc { get; init; }
    public string Kind { get; init; } = GeofenceActivityKind.Entered;
    public int? VehicleId { get; init; }
    public string? VehicleLabel { get; init; }
    public string? DriverName { get; init; }
    public string? Message { get; init; }
}

public sealed class GeofenceVisitDto
{
    public long Id { get; init; }
    public int GeofenceId { get; init; }
    public string GeofenceName { get; init; } = string.Empty;
    public int VehicleId { get; init; }
    public string VehicleLabel { get; init; } = string.Empty;
    public int? DriverId { get; init; }
    public string? DriverName { get; init; }
    public DateTime EnteredUtc { get; init; }
    public DateTime? ExitedUtc { get; init; }
    public int DurationMinutes { get; init; }
    public int IdleMinutes { get; init; }
    public decimal MaxSpeedKmh { get; init; }
    public string Status { get; init; } = GeofenceVisitStatus.Completed;
}

public sealed class GeofenceVisitsPageDto
{
    public IReadOnlyList<GeofenceVisitDto> Items { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>Visits report KPIs and today's hourly occupancy, for the same filters as the table.</summary>
public sealed class GeofenceVisitsSummaryDto
{
    public int Visits { get; init; }
    public int Vehicles { get; init; }
    public int TotalMinutes { get; init; }
    public double? AvgMinutes { get; init; }
    public int IdleMinutes { get; init; }
    public int Breaches { get; init; }
    public int Overstays { get; init; }
    public int Overspeeds { get; init; }
    public int AfterHoursExits { get; init; }
    public int IdleBreaches { get; init; }
    /// <summary>The selected geofence's dwell limit, shown as the target; null when several or none.</summary>
    public int? DwellTargetMinutes { get; init; }
    /// <summary>Vehicles inside during each local hour of today (24 values).</summary>
    public IReadOnlyList<int> OccupancyToday { get; init; } = [];
    /// <summary>True for hours of today in which a dwell rule was breached (24 values).</summary>
    public IReadOnlyList<bool> DwellBreachHoursToday { get; init; } = [];
}

/// <summary>How the visits report groups its rows (TypeScript: GeofenceVisitGroupBy). A plain list of visits is the paged table.</summary>
public static class GeofenceVisitGroupBy
{
    public const string Vehicle = "vehicle";
    public const string Driver = "driver";
    public const string Geofence = "geofence";
    public const string Day = "day";

    public static readonly IReadOnlyList<string> Values = [Vehicle, Driver, Geofence, Day];
}

/// <summary>One group of the visits report: a vehicle, a driver, a geofence or a local day.</summary>
public sealed class GeofenceVisitGroupDto
{
    /// <summary>The vehicle, driver or geofence id; the local day as yyyy-MM-dd; "none" for visits without a driver.</summary>
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Visits { get; init; }
    public int Vehicles { get; init; }
    public int InsideNow { get; init; }
    /// <summary>Open visits count up to now, as in the report KPIs.</summary>
    public int TotalMinutes { get; init; }
    public double? AvgMinutes { get; init; }
    public int IdleMinutes { get; init; }
    public int Breaches { get; init; }
}

public sealed class GeofenceVisitGroupsDto
{
    public string GroupBy { get; init; } = GeofenceVisitGroupBy.Vehicle;
    public IReadOnlyList<GeofenceVisitGroupDto> Items { get; init; } = [];
    /// <summary>True when more visits matched than one report reads; the groups then cover the newest visits only.</summary>
    public bool Capped { get; init; }
    public int VisitsRead { get; init; }
}

public sealed class TestGeofenceShapeDto
{
    public string AreaType { get; init; } = "circle";
    public double CenterLat { get; init; }
    public double CenterLng { get; init; }
    public double RadiusMeters { get; init; }
    public string? GeoJson { get; init; }
    public int? CorridorWidthMeters { get; init; }
    public int BufferMeters { get; init; } = 25;
    /// <summary>Only these vehicles (the "applies to" choice); null = every vehicle of the company.</summary>
    public int[]? VehicleIds { get; init; }
}

public sealed class TestGeofenceShapeResultDto
{
    public int Entries { get; init; }
    public int Vehicles { get; init; }
    public int Days { get; init; }
    /// <summary>True when the position cap was reached; the count is then a lower bound.</summary>
    public bool Capped { get; init; }
}

public sealed class GeofenceGroupDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public int GeofenceCount { get; init; }
}

public sealed class SaveGeofenceGroupDto
{
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public sealed class GeofenceTypeDto
{
    public int? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Color { get; init; } = "#0f766e";
    public bool IsRestricted { get; init; }
    public bool IsBuiltIn { get; init; }
}

public sealed class CreateGeofenceTypeDto
{
    public string Name { get; init; } = string.Empty;
    public string Color { get; init; } = "#0f766e";
    public bool IsRestricted { get; init; }
}

public sealed class GeofenceRecipientOptionDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class GeofenceRecipientOptionsDto
{
    public IReadOnlyList<GeofenceRecipientOptionDto> Roles { get; init; } = [];
    public IReadOnlyList<GeofenceRecipientOptionDto> Users { get; init; } = [];
}

/// <summary>Bulk actions from the list's tick boxes (TypeScript: GeofenceBulkAction).</summary>
public static class GeofenceBulkAction
{
    public const string Assign = "assign";
    public const string Group = "group";
    public const string Enable = "enable";
    public const string Disable = "disable";
    public const string Delete = "delete";

    public static readonly IReadOnlyList<string> Values = [Assign, Group, Enable, Disable, Delete];
}

public sealed class BulkGeofenceActionDto
{
    public int[] Ids { get; init; } = [];
    public string Action { get; init; } = string.Empty;
    /// <summary>For "group": the target group, or null to ungroup.</summary>
    public int? GroupId { get; init; }
    /// <summary>For "assign": vehicles added to each geofence's existing assignments.</summary>
    public int[]? VehicleIds { get; init; }
}

public sealed class BulkGeofenceResultDto
{
    public int Affected { get; init; }
}

public sealed class ImportGeofencesDto
{
    public SaveGeofenceDto[] Items { get; init; } = [];
}

public sealed class ImportGeofencesResultDto
{
    public int Created { get; init; }
    public IReadOnlyList<ImportSkippedDto> Skipped { get; init; } = [];
}

public sealed class ImportSkippedDto
{
    public string Name { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
