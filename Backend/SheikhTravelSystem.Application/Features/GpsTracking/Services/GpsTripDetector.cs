using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class GpsTripDetector
{
    private const decimal MovingSpeedKmh = 5m;
    private const int StopMinutesThreshold = 5;

    public static List<GpsTripDto> DetectTrips(
        int vehicleId,
        string? vehicleName,
        int? gpsDeviceId,
        IReadOnlyList<PositionDto> points)
    {
        if (points.Count < 2)
        {
            return [];
        }

        var ordered = points.OrderBy(p => p.Timestamp).ToList();
        var trips = new List<GpsTripDto>();
        DateTime? tripStart = null;
        double segmentDistance = 0;
        var segmentSpeeds = new List<decimal>();
        PositionDto? prev = null;

        void CloseTrip(PositionDto endPoint)
        {
            if (tripStart is null || prev is null)
            {
                return;
            }

            var duration = (int)Math.Max(1, (endPoint.Timestamp - tripStart.Value).TotalMinutes);
            trips.Add(new GpsTripDto(
                vehicleId,
                vehicleName,
                gpsDeviceId,
                tripStart.Value,
                endPoint.Timestamp,
                Math.Round(segmentDistance, 2),
                segmentSpeeds.Count > 0 ? Math.Round(segmentSpeeds.Average(), 1) : 0,
                segmentSpeeds.Count > 0 ? segmentSpeeds.Max() : 0,
                duration));

            tripStart = null;
            segmentDistance = 0;
            segmentSpeeds.Clear();
        }

        foreach (var point in ordered)
        {
            if (prev is not null)
            {
                segmentDistance += GpsGeoHelper.HaversineKm(
                    prev.Latitude, prev.Longitude, point.Latitude, point.Longitude);
            }

            var moving = point.Ignition == true || point.Speed > MovingSpeedKmh;
            var stopped = point.Ignition == false || point.Speed <= MovingSpeedKmh;

            if (tripStart is null && moving)
            {
                tripStart = point.Timestamp;
                segmentDistance = 0;
                segmentSpeeds.Clear();
            }

            if (tripStart is not null)
            {
                if (point.Speed > 0)
                {
                    segmentSpeeds.Add(point.Speed);
                }

                if (stopped && prev is not null)
                {
                    var idleMinutes = (point.Timestamp - prev.Timestamp).TotalMinutes;
                    if (idleMinutes >= StopMinutesThreshold)
                    {
                        CloseTrip(prev);
                    }
                }
            }

            prev = point;
        }

        if (tripStart is not null && prev is not null)
        {
            CloseTrip(prev);
        }

        return trips;
    }
}
