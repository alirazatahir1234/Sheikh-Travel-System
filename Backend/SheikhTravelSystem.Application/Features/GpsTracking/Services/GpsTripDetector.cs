using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class GpsTripDetector
{
    private const decimal MovingSpeedKmh = 10m;
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
        PositionDto? startPoint = null;
        double segmentDistance = 0;
        var segmentSpeeds = new List<decimal>();
        PositionDto? prev = null;

        void CloseTrip(PositionDto endPoint)
        {
            if (tripStart is null || prev is null || startPoint is null)
            {
                return;
            }

            var duration = (int)Math.Max(1, (endPoint.Timestamp - tripStart.Value).TotalMinutes);
            var start = tripStart.Value;
            var distanceKm = Math.Round(segmentDistance, 2);
            var reportedAvg = segmentSpeeds.Count > 0 ? (double)segmentSpeeds.Average() : 0;
            var reportedMax = segmentSpeeds.Count > 0 ? (double)segmentSpeeds.Max() : 0;
            var (avg, max, status) = TraccarTripMapper.NormalizeSpeeds(
                distanceKm, duration, reportedAvg, reportedMax);
            trips.Add(new GpsTripDto(
                vehicleId,
                vehicleName,
                gpsDeviceId,
                start,
                endPoint.Timestamp,
                distanceKm,
                avg,
                max,
                duration,
                TripKey: TripKeyHelper.Build(vehicleId, start),
                Status: status,
                StartLatitude: startPoint.Latitude,
                StartLongitude: startPoint.Longitude,
                EndLatitude: endPoint.Latitude,
                EndLongitude: endPoint.Longitude));

            tripStart = null;
            startPoint = null;
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

            var moving = point.Ignition != false && point.Speed >= MovingSpeedKmh;
            var stopped = point.Ignition == false || point.Speed < MovingSpeedKmh;

            if (tripStart is null && moving)
            {
                tripStart = point.Timestamp;
                startPoint = point;
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
