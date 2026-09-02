using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>Maps replay bundles to display-friendly history report fields.</summary>
public static class HistoryReplayMapper
{
    public const int ParkingStopMinutes = 120;
    public const decimal MovingSpeedThresholdKmh = 3m;

    public static IReadOnlyList<TripStopDto> SplitParkingStops(IReadOnlyList<TripStopDto> stops)
        => stops.Where(s => s.DurationMinutes >= ParkingStopMinutes).ToList();

    public static string DerivePositionStatus(decimal speedKmh, bool? ignition)
    {
        if (speedKmh >= MovingSpeedThresholdKmh) return "moving";
        if (ignition == true) return "idle";
        return "stopped";
    }

    public static HistoryPositionDto ToHistoryPosition(TripReplayPositionDto p) =>
        new(
            p.Latitude,
            p.Longitude,
            p.SpeedKmh,
            p.Heading,
            p.Timestamp,
            p.Ignition,
            DerivePositionStatus(p.SpeedKmh, p.Ignition));

    public static IReadOnlyList<HistoryPositionDto> MapPositions(IReadOnlyList<TripReplayPositionDto> route)
        => route.Select(ToHistoryPosition).ToList();

    public static HistoryDisplaySummaryDto BuildDisplaySummary(
        TripAnalyticsSummaryDto statistics,
        IReadOnlyList<TripStopDto> stops,
        double? mileageKm)
    {
        var parking = SplitParkingStops(stops);
        var distance = mileageKm ?? statistics.DistanceKm;

        return new HistoryDisplaySummaryDto(
            GpsDurationFormatter.FormatMinutes(statistics.DrivingMinutes),
            GpsDurationFormatter.FormatMinutes(statistics.IdleMinutes),
            statistics.StopCount > 0 ? statistics.StopCount : stops.Count,
            parking.Count,
            statistics.MaxSpeedKmh,
            distance);
    }

    public static HistoryReplayBundleDto WithDisplayFields(
        HistoryReplayBundleDto bundle,
        int? traccarDeviceId,
        string? deviceName,
        DateTime fromDate,
        DateTime toDate)
    {
        var statistics = bundle.Statistics;
        if (statistics is null)
            return bundle;

        var parking = SplitParkingStops(bundle.Stops);
        var displaySummary = BuildDisplaySummary(statistics, bundle.Stops, bundle.MileageKm);
        var positions = MapPositions(bundle.Route);

        var resolvedName = deviceName ?? bundle.Vehicle?.DeviceName ?? bundle.Vehicle?.VehicleName;

        return bundle with
        {
            DeviceId = traccarDeviceId,
            DeviceName = resolvedName,
            From = fromDate,
            To = toDate,
            DisplaySummary = displaySummary,
            Positions = positions,
            Parking = parking
        };
    }
}
