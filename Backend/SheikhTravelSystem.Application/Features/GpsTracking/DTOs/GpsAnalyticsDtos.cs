namespace SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

/// <summary>
/// Overview KPI cards for the Analytics dashboard. "Fleet status" fields are a live snapshot
/// (independent of the filter range); "range totals" respect the caller's From/To/Branch/
/// Department/Driver filters; "today" fields are always calendar-day-scoped regardless of the
/// selected range, matching how existing GPS stats endpoints (e.g. GpsAlertStatsDto.Today,
/// GeofenceStatsDto.TodayEntries) already treat "today" as its own fixed window.
/// </summary>
public record AnalyticsOverviewDto(
    int TotalVehicles,
    int Online,
    int Offline,
    int Moving,
    int Idle,
    int Stopped,
    decimal DistanceKm,
    int DrivingMinutes,
    int IdleMinutes,
    decimal AvgSpeedKmh,
    decimal MaxSpeedKmh,
    decimal? FuelLiters,
    decimal? EngineHours,
    int TripsToday,
    int StopsToday,
    int GeofenceEntriesToday,
    int OverspeedEventsToday,
    /// <summary>
    /// Live (Moving+Idle)/Total estimate from the current fleet-status snapshot — NOT the
    /// rigorous time-window utilization the dedicated Utilization section computes from
    /// GpsFleetStatusSnapshots history. Kept distinct on purpose; do not conflate the two.
    /// </summary>
    decimal? UtilizationPercent);

public record DailyMetricDto(DateTime Date, decimal DistanceKm, int TripCount, decimal AvgSpeedKmh);

public record DistanceAnalyticsDto(List<DailyMetricDto> Daily, decimal TotalDistanceKm, int TotalTrips);

public record SpeedHistogramBucketDto(string Label, int Count);

public record SpeedAnalyticsDto(
    decimal AvgSpeedKmh,
    decimal MaxSpeedKmh,
    List<SpeedHistogramBucketDto> Histogram,
    List<DailyMetricDto> DailyAvgSpeed);

public record VehicleIdleDto(int VehicleId, string? VehicleName, int IdleMinutes);

/// <summary>
/// Idle/Stop data is Traccar-only (no local idle/stop detector exists). IsPartial is true when at
/// least one vehicle in the filtered set has no Traccar link at all — those vehicles simply cannot
/// report idle/stop data, so the totals below undercount the true fleet. Surfaced explicitly rather
/// than silently shown as complete.
/// </summary>
public record IdleAnalyticsDto(
    int TotalIdleMinutes,
    int LongestIdleMinutes,
    List<VehicleIdleDto> TopIdleVehicles,
    bool IsPartial);

public record StopAnalyticsDto(
    int TotalStops,
    decimal AvgStopDurationMinutes,
    int LongestStopMinutes,
    bool IsPartial);

public record DriverScoreFactorsDto(
    int TripCount,
    decimal DistanceKm,
    int OverspeedCount,
    int IdleMinutes,
    int HarshEventCount,
    decimal NightDrivingPercent,
    decimal? FuelLiters);

/// <summary>
/// IsPartial is true when idle/harsh-event factors couldn't be computed for this driver (no
/// Traccar-linked vehicle in their assigned trips, or Traccar unreachable/disabled) — the score
/// still reflects overspeed/distance/night-driving, just without those two inputs. Surfaced so the
/// ranking table can flag it rather than presenting an artificially-clean score.
/// </summary>
public record DriverScoreDto(
    int DriverId,
    string DriverName,
    int Score,
    string Rating,
    DriverScoreFactorsDto Factors,
    bool IsPartial);

public record DailyUtilizationDto(DateTime Date, decimal UtilizationPercent);

/// <summary>
/// Hours fields are vehicle-hours approximated from hourly GpsFleetStatusSnapshots (count-in-state
/// per snapshot × ~1h interval, summed) — a coarse but cheap estimate, not a precise timer.
/// UtilizationPercent follows the spec's literal formula (Running Time / Total Available Time),
/// distinct from AnalyticsOverviewDto.UtilizationPercent's live (Moving+Idle)/Total instant estimate.
/// </summary>
public record FleetUtilizationDto(
    decimal RunningHours,
    decimal IdleHours,
    decimal ParkingHours,
    decimal OfflineHours,
    decimal UtilizationPercent,
    string UtilizationLabel,
    List<DailyUtilizationDto> Daily);

public record DailyFuelDto(DateTime Date, decimal Liters, decimal Cost);

public record VehicleFuelDto(
    int VehicleId,
    string? VehicleName,
    decimal Liters,
    decimal Cost,
    decimal? DistanceKm,
    decimal? LitersPer100Km);

/// <summary>
/// First code in this codebase to cross-reference FuelLogs (manual fuel entries) with GpsTrips
/// (distance) — the two data sources were previously completely unlinked. FuelLitersPer100Km is
/// null wherever a vehicle has fuel entries but no trip distance in the same range (division by
/// zero avoided, not silently shown as 0).
/// </summary>
public record FuelAnalyticsDto(
    decimal TotalLiters,
    decimal TotalCost,
    decimal? FleetLitersPer100Km,
    List<DailyFuelDto> Daily,
    List<VehicleFuelDto> ByVehicle);

/// <summary>Dwell time is paired from consecutive enter→exit events per vehicle+geofence; only geofences with at least one event in range appear (no zero-visit rows).</summary>
public record GeofenceVisitDto(int GeofenceId, string GeofenceName, int EntryCount, int ExitCount, decimal? AvgDwellMinutes);

public record GeofenceAnalyticsDto(
    List<GeofenceVisitDto> MostVisited,
    List<GeofenceVisitDto> LeastVisited,
    int TotalEntries,
    int TotalExits);

public record EventTypeCountDto(string Key, int Count);

public record DailyEventCountDto(DateTime Date, int Count);

/// <summary>ByType is normalized via GpsEventTypeNormalizer so spelling variants (overspeed/speed_exceeded, etc.) collapse into one bucket rather than splitting a single event family.</summary>
public record AlertEventStatsDto(
    List<EventTypeCountDto> ByType,
    List<EventTypeCountDto> BySeverity,
    List<DailyEventCountDto> Daily,
    int Total);

public record HeatmapPointDto(double Latitude, double Longitude, int Count);

/// <summary>
/// MaintenanceStatus is "None" when the vehicle has no active maintenance schedule (not itself a
/// problem). Insurance/warranty status buckets: Valid / ExpiringSoon (within 30 days) / Expired / Unknown (no date on record).
/// </summary>
public record GpsVehicleHealthDto(
    int VehicleId,
    string VehicleName,
    decimal? BatteryLevel,
    int? GpsSignal,
    string MaintenanceStatus,
    DateTime? InsuranceExpiryDate,
    string InsuranceStatus,
    DateTime? TrackerWarrantyEnd,
    string TrackerWarrantyStatus);

public record VehicleRankingDto(
    int VehicleId,
    string VehicleName,
    decimal DistanceKm,
    int TripCount,
    decimal AvgSpeedKmh,
    decimal FuelCost,
    decimal MaintenanceCost,
    decimal? CostPerKm);

/// <summary>CostBasisNote is rendered verbatim in the UI — Cost/KM = (Fuel + Maintenance) / Distance only; toll/parking/driver cost have no data source in this codebase and are explicitly excluded, not silently zeroed.</summary>
public record CostAnalyticsDto(
    decimal TotalFuelCost,
    decimal TotalMaintenanceCost,
    decimal TotalCost,
    decimal TotalDistanceKm,
    decimal? CostPerKm,
    string CostBasisNote);

public record TrendPointDto(DateTime Period, decimal DistanceKm, int TripCount, int OverspeedCount);

public record TrendsDto(List<TrendPointDto> Points);

public record ComparativePeriodDto(string Label, decimal DistanceKm, int TripCount, decimal AvgSpeedKmh, int OverspeedCount);

public record ComparativeAnalyticsDto(ComparativePeriodDto PeriodA, ComparativePeriodDto? PeriodB);

public record AnalyticsReportFiltersDto(int? BranchId, int? DepartmentId, string? From, string? To);

public record AnalyticsReportScheduleDto(
    int Id,
    string ReportType,
    AnalyticsReportFiltersDto Filters,
    string Frequency,
    string Recipients,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    string? LastRunStatus,
    bool IsActive);

public record CreateAnalyticsReportScheduleDto(
    string ReportType,
    AnalyticsReportFiltersDto Filters,
    string Frequency,
    string Recipients);

public record UpdateAnalyticsReportScheduleDto(
    string? Frequency,
    string? Recipients,
    bool? IsActive,
    AnalyticsReportFiltersDto? Filters);
