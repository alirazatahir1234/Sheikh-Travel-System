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

public record TripDetailBundleDto(
    GpsTripDto Trip,
    TripReplaySummaryDto? Summary,
    IReadOnlyList<TripStopDto> Stops,
    IReadOnlyList<TripEventDto> Events,
    IReadOnlyList<TripReplayPositionDto> Route,
    IReadOnlyList<TripReplayPositionDto> Playback);

public record TripReplaySummaryDto(
    double DistanceKm,
    int DrivingMinutes,
    decimal AvgSpeedKmh,
    decimal MaxSpeedKmh,
    decimal? FuelLiters,
    decimal? EngineHours = null);

/// <summary>
/// Traccar-sourced fleet snapshot — calls the live Traccar server directly and returns all-zeros
/// if Traccar isn't configured/enabled. NOT the same data source as <see cref="GpsFleetStatusLocalDto"/>
/// (which reads local tables) — do not conflate or merge these two, they intentionally differ.
/// </summary>
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

/// <summary>
/// Local-data-sourced fleet snapshot (Vehicles + VehicleCurrentLocation), independent of Traccar
/// reachability — this is what the Live Map screen's KPI strip actually uses, matching the same
/// status-derivation rule as the frontend's resolveFleetStatus()/GetLivePositionsQuery. Computed
/// by the shared GpsFleetStatusCalculator so this and the background snapshot job never drift.
/// Online = Moving + Idle + Parked + Sos (i.e. everything that isn't Offline or NeverSeen).
/// </summary>
public record GpsFleetStatusLocalDto(
    int TotalVehicles,
    int Online,
    int Offline,
    int Moving,
    int Idle,
    int Parked,
    int NeverSeen,
    int Sos,
    int AlertsToday);

/// <summary>Single poll payload for GPS Operator mobile control-room dashboard.</summary>
public record GpsOperatorDashboardDto(
    GpsFleetStatusLocalDto Fleet,
    int TrackerHealthy,
    int TrackerOffline,
    int WeakGsm,
    int LowBattery,
    int NoGpsSignal,
    int IgnitionOn,
    int OverspeedAlertsToday,
    int SosAlertsToday,
    int GeofenceAlertsToday,
    int OfflineAlertsToday,
    int PowerCutAlertsToday,
    int TodaysTrips,
    double? TodayDistanceKm);

public record GpsOperatorInsightDto(
    string Title,
    string Summary,
    IReadOnlyList<string> Bullets);

public record GpsFleetStatusSnapshotDto(
    DateTime SnapshotAt,
    int TotalVehicles,
    int Online,
    int Offline,
    int Moving,
    int Idle,
    int Parked,
    int NeverSeen,
    int Sos,
    int AlertsToday);

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
    int? Satellites,
    decimal? TotalDistanceKm = null);

public record TripEventDto(
    DateTime Time,
    string Type,
    double? Latitude,
    double? Longitude,
    string? Address,
    decimal? SpeedKmh,
    int? GeofenceId = null,
    string? GeofenceName = null,
    string? Label = null);

/// <summary>Human-readable history summary for route playback UI (formatted durations).</summary>
public record HistoryDisplaySummaryDto(
    string MovingTime,
    string NonMovingTime,
    int Stops,
    int Parking,
    decimal MaxSpeed,
    double Distance);

/// <summary>GPS point for history route line with motion status.</summary>
public record HistoryPositionDto(
    double Latitude,
    double Longitude,
    decimal Speed,
    double? Course,
    DateTime Timestamp,
    bool? Ignition,
    string Status);

public record HistoryReplayBundleDto(
    IReadOnlyList<TripReplayPositionDto> Route,
    IReadOnlyList<TripReplayPositionDto> Playback,
    IReadOnlyList<TripStopDto> Stops,
    IReadOnlyList<TripEventDto> Events,
    TripReplaySummaryDto? Summary,
    TripAnalyticsSummaryDto? Statistics,
    double? MileageKm,
    TripDeviceContextDto? Vehicle,
    int? DeviceId = null,
    string? DeviceName = null,
    DateTime? From = null,
    DateTime? To = null,
    HistoryDisplaySummaryDto? DisplaySummary = null,
    IReadOnlyList<HistoryPositionDto>? Positions = null,
    IReadOnlyList<TripStopDto>? Parking = null);

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
