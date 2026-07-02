namespace SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

public record TripAnalyticsSummaryDto(
    int TripCount,
    double DistanceKm,
    int DrivingMinutes,
    int IdleMinutes,
    decimal? FuelLiters,
    decimal AvgSpeedKmh,
    decimal MaxSpeedKmh,
    int StopCount,
    int OverspeedCount,
    int HarshBrakeCount,
    int HarshAccelCount,
    decimal? EngineHours = null);

public record TripAnalyticsBundleDto(
    TripAnalyticsSummaryDto Summary,
    IReadOnlyList<TripEventDto> Events,
    IReadOnlyList<TripStopDto> Stops);

public record TripReplayBundleDto(
    IReadOnlyList<TripReplayPositionDto> Route,
    IReadOnlyList<TripReplayPositionDto> Playback,
    IReadOnlyList<TripStopDto> Stops,
    IReadOnlyList<TripEventDto> Events,
    TripReplaySummaryDto? Summary);

public record TripReplaySummaryDto(
    double DistanceKm,
    int DrivingMinutes,
    decimal AvgSpeedKmh,
    decimal MaxSpeedKmh,
    decimal? FuelLiters,
    decimal? EngineHours = null);

public record GpsFleetStatusDto(
    int TotalVehicles,
    int Online,
    int Offline,
    int Moving,
    int Idle,
    int Parked,
    int NeverSeen,
    double? AvgSpeedKmh,
    double? TodayDistanceKm);

public record TripReplayPositionDto(
    DateTime Timestamp,
    double Latitude,
    double Longitude,
    decimal SpeedKmh,
    double? Heading,
    bool? Ignition,
    double? Altitude,
    string? Address,
    decimal? BatteryLevel,
    int? Satellites);

public record TripEventDto(
    DateTime Time,
    string Type,
    double? Latitude,
    double? Longitude,
    string? Address,
    decimal? SpeedKmh);

public record TripStopDto(
    DateTime StartTime,
    DateTime EndTime,
    double Latitude,
    double Longitude,
    string? Address,
    int DurationMinutes);

public record TripDeviceContextDto(
    int VehicleId,
    string? VehicleName,
    string? PlateNumber,
    int? GpsDeviceId,
    string? DeviceName,
    string? UniqueId,
    bool HasTraccarLink,
    bool IsOnline,
    DateTime? LastPositionAt,
    double? LastLatitude,
    double? LastLongitude,
    string? LastAddress,
    decimal? LastSpeedKmh,
    bool? LastIgnition);
