namespace SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

public record PositionDto(
    int Id,
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
    DateTime Timestamp);

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
    bool? Ignition = null);

public record GpsDeviceDto(
    int Id,
    int? VehicleId,
    string? VehicleName,
    string UniqueId,
    string Name,
    string? Protocol,
    bool SupportsEngineCutoff,
    bool? LastIgnition,
    DateTime? LastSeenAt,
    bool IsActive);

public record CreateGpsDeviceDto(
    int? VehicleId,
    string UniqueId,
    string Name,
    string? Protocol,
    bool SupportsEngineCutoff);

public record UpdateGpsDeviceDto(
    int? VehicleId,
    string Name,
    string? Protocol,
    bool SupportsEngineCutoff,
    bool IsActive);

public record GeofenceDto(
    int Id,
    string Name,
    string AreaType,
    double CenterLat,
    double CenterLng,
    double RadiusMeters,
    string? GeoJson,
    bool IsActive);

public record CreateGeofenceDto(
    string Name,
    double CenterLat,
    double CenterLng,
    double RadiusMeters,
    string? GeoJson = null);

public record UpdateGeofenceDto(
    string Name,
    double CenterLat,
    double CenterLng,
    double RadiusMeters,
    string? GeoJson,
    bool IsActive);

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
    bool IsAcknowledged);

public record GpsTripDto(
    int VehicleId,
    string? VehicleName,
    int? GpsDeviceId,
    DateTime StartTime,
    DateTime EndTime,
    double DistanceKm,
    decimal AvgSpeedKmh,
    decimal MaxSpeedKmh,
    int DurationMinutes);

public record GpsDeviceCommandDto(
    int Id,
    int GpsDeviceId,
    string? DeviceName,
    string CommandType,
    string Status,
    string? RequestedBy,
    DateTime RequestedAt,
    DateTime? CompletedAt);

public record SendDeviceCommandDto(int GpsDeviceId, string CommandType);

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
