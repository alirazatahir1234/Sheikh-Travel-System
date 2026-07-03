using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Derives stationary-period "stops" from local position history — a last-resort fallback for
/// the fleet-wide Stops report when a vehicle is neither Traccar-linked nor covered by the
/// capped Traccar fan-out. Mirrors GpsTripDetector's idle-threshold concept, but exposes stops
/// as first-class output instead of only using them internally to decide when to close a trip.
/// </summary>
public static class GpsStopDetector
{
    private const decimal StoppedSpeedKmh = 5m;
    private const int MinStopMinutes = 5;

    public static List<TripStopDto> DetectStops(IReadOnlyList<PositionDto> points)
    {
        if (points.Count < 2)
        {
            return [];
        }

        var ordered = points.OrderBy(p => p.Timestamp).ToList();
        var stops = new List<TripStopDto>();
        PositionDto? stopStart = null;
        PositionDto? prev = null;

        void CloseStop(PositionDto endPoint)
        {
            if (stopStart is null)
            {
                return;
            }

            var durationMinutes = (int)Math.Max(1, (endPoint.Timestamp - stopStart.Timestamp).TotalMinutes);
            if (durationMinutes >= MinStopMinutes)
            {
                stops.Add(new TripStopDto(
                    stopStart.Timestamp,
                    endPoint.Timestamp,
                    stopStart.Latitude,
                    stopStart.Longitude,
                    null,
                    durationMinutes));
            }

            stopStart = null;
        }

        foreach (var point in ordered)
        {
            var stopped = point.Ignition == false || point.Speed <= StoppedSpeedKmh;

            if (stopped)
            {
                stopStart ??= point;
            }
            else if (stopStart is not null && prev is not null)
            {
                CloseStop(prev);
            }

            prev = point;
        }

        if (stopStart is not null && prev is not null)
        {
            CloseStop(prev);
        }

        return stops;
    }
}
