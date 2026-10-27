using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Builds one trip per calendar day: start at first movement, end at last stop after movement.
/// Used for tenants in <see cref="FleetTripCreationMode.AutoGpsDaily"/> — separate from segment-based
/// <see cref="GpsTripDetector"/> used in booking mode.
/// </summary>
public static class GpsDailyTripDetector
{
    private const decimal MovingSpeedKmh = 10m;

    public static GpsTripDto? DetectDailyTrip(
        int vehicleId,
        string? vehicleName,
        int? gpsDeviceId,
        IReadOnlyList<PositionDto> points,
        DateOnly localDate,
        TimeZoneInfo timeZone)
    {
        if (points.Count == 0)
        {
            return null;
        }

        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue), timeZone);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(localDate.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);

        var inDay = points
            .Where(p => p.Timestamp >= dayStartUtc && p.Timestamp < dayEndUtc)
            .OrderBy(p => p.Timestamp)
            .ToList();

        if (inDay.Count == 0)
        {
            return null;
        }

        PositionDto? startPoint = null;
        PositionDto? lastStop = null;
        var sawMovement = false;

        foreach (var point in inDay)
        {
            if (IsMoving(point))
            {
                startPoint ??= point;
                sawMovement = true;
            }
            else if (sawMovement && IsStopped(point))
            {
                lastStop = point;
            }
        }

        if (!sawMovement || startPoint is null)
        {
            return null;
        }

        var endPoint = lastStop ?? inDay[^1];
        if (endPoint.Timestamp < startPoint.Timestamp)
        {
            endPoint = startPoint;
        }

        var slice = inDay
            .Where(p => p.Timestamp >= startPoint.Timestamp && p.Timestamp <= endPoint.Timestamp)
            .ToList();

        double segmentDistance = 0;
        var segmentSpeeds = new List<decimal>();
        PositionDto? prev = null;
        foreach (var point in slice)
        {
            if (prev is not null)
            {
                segmentDistance += GpsGeoHelper.HaversineKm(
                    prev.Latitude, prev.Longitude, point.Latitude, point.Longitude);
            }

            if (point.Speed > 0)
            {
                segmentSpeeds.Add(point.Speed);
            }

            prev = point;
        }

        var duration = (int)Math.Max(1, (endPoint.Timestamp - startPoint.Timestamp).TotalMinutes);
        var distanceKm = Math.Round(segmentDistance, 2);
        var reportedAvg = segmentSpeeds.Count > 0 ? (double)segmentSpeeds.Average() : 0;
        var reportedMax = segmentSpeeds.Count > 0 ? (double)segmentSpeeds.Max() : 0;
        var (avg, max, status) = TraccarTripMapper.NormalizeSpeeds(
            distanceKm, duration, reportedAvg, reportedMax);

        return new GpsTripDto(
            vehicleId,
            vehicleName,
            gpsDeviceId,
            startPoint.Timestamp,
            endPoint.Timestamp,
            distanceKm,
            avg,
            max,
            duration,
            TripKey: TripKeyHelper.Build(vehicleId, startPoint.Timestamp),
            Status: status,
            StartLatitude: startPoint.Latitude,
            StartLongitude: startPoint.Longitude,
            EndLatitude: endPoint.Latitude,
            EndLongitude: endPoint.Longitude);
    }

    private static bool IsMoving(PositionDto point) =>
        point.Ignition != false && point.Speed >= MovingSpeedKmh;

    private static bool IsStopped(PositionDto point) =>
        point.Ignition == false || point.Speed < MovingSpeedKmh;
}
