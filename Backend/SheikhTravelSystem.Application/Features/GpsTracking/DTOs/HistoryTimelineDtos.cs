namespace SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

/// <summary>
/// Segment kinds sent to clients as strings. Mirrored by the ERP union <c>HistorySegmentKind</c>
/// in <c>gps-tracking.model.ts</c> — change one, change the other.
/// </summary>
public static class HistorySegmentKinds
{
    public const string Trip = "trip";
    public const string Stop = "stop";
    public const string Parking = "parking";
}

/// <summary>
/// One calculation for the History workspace: the period cut into trips and standstills, the same
/// cut rolled up per local day, and the period totals. Days add up to the totals and a day's trips
/// add up to the day, by construction (see <c>HistoryTimelineBuilder</c>).
/// </summary>
public record HistoryTimelineDto(
    HistoryTimelineTotalsDto Totals,
    IReadOnlyList<HistoryTimelineDayDto> Days,
    IReadOnlyList<HistoryTimelineSegmentDto> Segments,
    /// <summary>A standstill at least this long ends a trip; shorter ones stay inside the trip.</summary>
    int MinStopMinutes,
    /// <summary>A standstill at least this long is parking, a shorter one is a stop.</summary>
    int ParkingMinutes);

public record HistoryTimelineTotalsDto(
    /// <summary>Sum of the trips' distances (one decimal).</summary>
    decimal DistanceKm,
    /// <summary>Sum of the trips' durations.</summary>
    int DrivingMinutes,
    /// <summary>WindowMinutes − DrivingMinutes.</summary>
    int StoppedMinutes,
    int WindowMinutes,
    int TripCount,
    /// <summary>Every standstill listed in the timeline: stops and parking together.</summary>
    int StopCount,
    /// <summary>The standstills of <see cref="HistoryTimelineDto.ParkingMinutes"/> or longer.</summary>
    int ParkingCount,
    decimal MaxSpeedKmh,
    /// <summary>DistanceKm ÷ driving time, never above MaxSpeedKmh.</summary>
    decimal AvgSpeedKmh,
    /// <summary>Driving minutes that are an estimate because no fix arrived (part of DrivingMinutes).</summary>
    int NoSignalMinutes);

public record HistoryTimelineDayDto(
    /// <summary>Local calendar day in the vehicle's timezone, yyyy-MM-dd.</summary>
    string Date,
    /// <summary>UTC start of the day, clipped to the window.</summary>
    DateTime StartTime,
    /// <summary>UTC end of the day, clipped to the window.</summary>
    DateTime EndTime,
    decimal DistanceKm,
    int DrivingMinutes,
    int TripCount,
    int StopCount,
    int ParkingCount);

public record HistoryTimelineSegmentDto(
    string Id,
    /// <summary>One of <see cref="HistorySegmentKinds"/>.</summary>
    string Kind,
    /// <summary>The local day the segment is listed under: the day it starts.</summary>
    string Date,
    DateTime StartTime,
    DateTime EndTime,
    int DurationMinutes,
    /// <summary>Trips only.</summary>
    decimal? DistanceKm,
    /// <summary>Trips only.</summary>
    decimal? MaxSpeedKmh,
    double StartLatitude,
    double StartLongitude,
    string? StartAddress,
    double EndLatitude,
    double EndLongitude,
    string? EndAddress,
    /// <summary>Trips only: standstills shorter than the minimum stop, kept inside the trip.</summary>
    IReadOnlyList<HistoryTimelineBriefStopDto>? BriefStops = null,
    /// <summary>Trips only: stretches driven without a fix, drawn dashed. Times are estimates.</summary>
    IReadOnlyList<HistoryTimelineNoSignalDto>? NoSignal = null,
    /// <summary>
    /// Standstills only: still under way when the window ended, in a window that ends now. The end
    /// is the window end and the duration is the time so far.
    /// </summary>
    bool Ongoing = false);

public record HistoryTimelineBriefStopDto(
    DateTime StartTime,
    DateTime EndTime,
    int DurationSeconds,
    double Latitude,
    double Longitude);

public record HistoryTimelineNoSignalDto(
    DateTime StartTime,
    DateTime EndTime,
    double StartLatitude,
    double StartLongitude,
    double EndLatitude,
    double EndLongitude,
    decimal DistanceKm);
