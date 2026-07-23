namespace SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

public record PositionDto(
    long Id,
    int VehicleId,
    int? DriverId,
    int? BookingId,
    int? GpsDeviceId,
    double Latitude,
    double Longitude,
    decimal Speed,
    double? Heading,
    double? Altitude,
    bool? Ignition,
    DateTime Timestamp,
    decimal? FuelLevel = null,
    decimal? BatteryLevel = null,
    int? GsmSignal = null,
    decimal? TotalDistanceKm = null,
    string? Address = null,
    string? AlarmType = null,
    string? DriverPhone = null,
    decimal? Temperature = null);

public record IngestPositionDto(
    int VehicleId,
    int? DriverId,
    int? BookingId,
    int? GpsDeviceId,
    double Latitude,
    double Longitude,
    decimal Speed,
    double? Heading = null,
    double? Altitude = null,
    bool? Ignition = null,
    decimal? FuelLevel = null,
    decimal? BatteryLevel = null,
    int? GsmSignal = null,
    decimal? TotalDistanceKm = null,
    string? Address = null,
    string? AlarmType = null,
    decimal? Temperature = null);

public record GpsDeviceDto(
    int Id,
    int? VehicleId,
    string? VehicleName,
    string? PlateNumber,
    string? DriverName,
    string UniqueId,
    string Name,
    string? Protocol,
    bool SupportsEngineCutoff,
    bool SupportsRelay,
    bool? LastIgnition,
    DateTime? LastSeenAt,
    bool IsActive,
    bool IsOnline,
    decimal? LastSpeed,
    decimal? LastBatteryLevel,
    int? LastRssi,
    int? TraccarDeviceId = null,
    bool IsTraccarLinked = false,
    bool IsValidImei = false,
    string? Model = null,
    string? SimNumber = null,
    string? Vendor = null,
    string? SerialNumber = null,
    DateTime? InstallationDate = null,
    string? InstalledBy = null,
    string? InstallationNotes = null,
    string? RelayOutput = null);

public record CreateGpsDeviceDto(
    int? VehicleId,
    string UniqueId,
    string Name,
    string? Protocol,
    bool SupportsEngineCutoff,
    string? Model = null,
    string? SimNumber = null,
    string? Vendor = null,
    string? SerialNumber = null,
    DateTime? InstallationDate = null,
    string? InstalledBy = null,
    string? InstallationNotes = null,
    string? RelayOutput = null);

public record UpdateGpsDeviceDto(
    int? VehicleId,
    string Name,
    string? Protocol,
    bool SupportsEngineCutoff,
    bool IsActive,
    string? SimNumber = null,
    string? SerialNumber = null,
    DateTime? InstallationDate = null,
    string? InstalledBy = null,
    string? InstallationNotes = null,
    string? RelayOutput = null);

public record GeofenceDto(
    int Id,
    string Name,
    string AreaType,
    double CenterLat,
    double CenterLng,
    double RadiusMeters,
    string? GeoJson,
    bool IsActive,
    string? Color = null,
    string? Category = null,
    string? Description = null,
    int AssignedVehicleCount = 0);

public record CreateGeofenceDto(
    string Name,
    string AreaType = "circle",
    double CenterLat = 0,
    double CenterLng = 0,
    double RadiusMeters = 0,
    string? GeoJson = null,
    string? Color = "#0f766e",
    string? Category = null,
    string? Description = null,
    bool IsActive = true);

public record UpdateGeofenceDto(
    string Name,
    string AreaType,
    double CenterLat,
    double CenterLng,
    double RadiusMeters,
    string? GeoJson,
    bool IsActive,
    string? Color = "#0f766e",
    string? Category = null,
    string? Description = null);

public record GeofenceAssignmentDto(
    int Id,
    int GeofenceId,
    int? VehicleId,
    string? VehicleName,
    string? RegistrationNumber,
    int? BranchId,
    string? BranchName,
    int? DepartmentId,
    string? DepartmentName);

public record UpsertGeofenceAssignmentsDto(
    int[]? VehicleIds = null,
    int? BranchId = null,
    int? DepartmentId = null,
    bool ReplaceVehicles = false);

public record GeofenceStatsDto(
    int Total,
    int Active,
    int VehiclesInside,
    int TodayEntries,
    int TodayExits,
    int UnackedBreaches);

public record GpsAlertRuleDto(
    int Id,
    int? VehicleId,
    string? VehicleName,
    decimal? SpeedLimitKmh,
    int? GeofenceId,
    string? GeofenceName,
    bool AlertOnEnter,
    bool AlertOnExit,
    bool IsActive);

public record CreateGpsAlertRuleDto(
    int? VehicleId,
    decimal? SpeedLimitKmh,
    int? GeofenceId,
    bool AlertOnEnter,
    bool AlertOnExit);

public record GpsAlertEventDto(
    int Id,
    int? RuleId,
    int VehicleId,
    string? VehicleName,
    string EventType,
    double Latitude,
    double Longitude,
    decimal Speed,
    string Message,
    DateTime Timestamp,
    bool IsAcknowledged,
    string Severity,
    string Status,
    int? GeofenceId = null,
    string? GeofenceName = null,
    int? DriverId = null,
    string? DriverName = null,
    DateTime? ReadAt = null,
    string? ReadBy = null,
    DateTime? AcknowledgedAt = null,
    string? AcknowledgedBy = null,
    DateTime? ResolvedAt = null,
    string? ResolvedBy = null,
    string? ResolutionNotes = null,
    DateTime? ArchivedAt = null,
    string? ArchivedBy = null,
    bool CanAcknowledge = false,
    bool CanResolve = false,
    bool CanArchive = false,
    bool CanDelete = false);

public record GpsAlertStatsDto(
    int Total,
    int Today,
    int Unread,
    int Active,
    int Resolved,
    int Critical,
    int Archived);

public record ResolveGpsAlertDto(string? ResolutionNotes);
public record ArchiveGpsAlertDto(string? ArchiveReason);

public record AlertSettingDto(
    string AlertType,
    bool InAppEnabled,
    bool EmailEnabled,
    bool PushEnabled,
    bool SmsEnabled);

public record UpdateAlertSettingsDto(List<AlertSettingDto> Settings);

/// <summary>Known GPS alert types shown in the notification settings matrix.</summary>
public static class AlertTypeCatalog
{
    public static readonly IReadOnlyList<string> Types =
    [
        "sos", "power_cut", "overspeed", "geofence_enter", "geofence_exit",
        "offline", "online", "ignition_on", "ignition_off", "low_battery",
        "gps_lost", "low_fuel"
    ];
}

public record GpsTripDto(
    int VehicleId,
    string? VehicleName,
    int? GpsDeviceId,
    DateTime StartTime,
    DateTime EndTime,
    double DistanceKm,
    decimal AvgSpeedKmh,
    decimal MaxSpeedKmh,
    int DurationMinutes,
    string? DeviceName = null,
    string? StartAddress = null,
    string? EndAddress = null,
    string? DriverName = null,
    decimal? FuelLiters = null,
    string? PlateNumber = null,
    string? TripKey = null,
    string? Status = null);

public record GpsDeviceCommandDto(
    int Id,
    int GpsDeviceId,
    string? DeviceName,
    string CommandType,
    string Status,
    string? RequestedBy,
    DateTime RequestedAt,
    DateTime? CompletedAt,
    int RetryCount = 0,
    int MaxRetries = 3,
    string? ErrorMessage = null,
    DateTime? NextRetryAt = null,
    string? Reason = null);

public record GpsCommandResponseDto(
    int Id,
    string Source,
    string? ResponseCode,
    string? ResponseText,
    DateTime ReceivedAt);

public record GpsDeviceCommandDetailDto(
    GpsDeviceCommandDto Command,
    string? AttributesJson,
    List<GpsCommandResponseDto> Responses);

public record SupportedCommandDto(
    string Type,
    string Label,
    bool Available,
    string? Reason);

public record CompleteDeviceCommandDto(
    string UniqueId,
    string Status,
    string? ResponseText = null,
    string? ErrorMessage = null);

public record SendDeviceCommandDto(
    int GpsDeviceId,
    string CommandType,
    string? Reason = null,
    Dictionary<string, object>? Attributes = null);

public record GpsEtaDto(
    int BookingId,
    int VehicleId,
    string? VehicleName,
    double DistanceKm,
    int? EtaMinutes,
    double DriverLatitude,
    double DriverLongitude,
    double PickupLatitude,
    double PickupLongitude);
