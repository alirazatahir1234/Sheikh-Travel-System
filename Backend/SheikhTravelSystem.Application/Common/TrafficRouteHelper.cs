using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common;

public static class TrafficRouteHelper
{
    public const string GoogleRoutes = "GoogleRoutes";
    public const string HaversineEstimate = "HaversineEstimate";

    public static bool ShouldNavigateToDropoff(TripStatus status) =>
        status is TripStatus.Enroute or TripStatus.Delayed;

    public static int? TrafficDelayMinutes(int durationSeconds, int? staticDurationSeconds)
    {
        if (staticDurationSeconds is null or <= 0 || durationSeconds <= 0)
            return null;
        var delay = durationSeconds - staticDurationSeconds.Value;
        if (delay <= 0) return 0;
        return (int)Math.Ceiling(delay / 60.0);
    }

    public static string? TrafficLevel(int durationSeconds, int? staticDurationSeconds)
    {
        if (staticDurationSeconds is null or <= 0 || durationSeconds <= 0)
            return null;
        var ratio = durationSeconds / (double)staticDurationSeconds.Value;
        return ratio switch
        {
            < 1.08 => "light",
            < 1.25 => "moderate",
            _ => "heavy"
        };
    }
}
