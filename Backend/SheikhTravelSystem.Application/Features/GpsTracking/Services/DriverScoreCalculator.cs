using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Pure scoring calculator — realistically scoped to signals reliably available fleet-wide
/// (overspeed + harsh from GpsAlertEvents.DriverId, distance/night-driving from GpsTrips via an
/// AssignmentHistory time-window join) plus Traccar idle (and live harsh fallback) when available.
/// Seatbelt is deliberately absent — no sensor data exists anywhere in this codebase for it.
///
/// Weights below are starting points (roughly equal-weighted penalty buckets), named and commented
/// so they're easy to find and tune — this is a business/product decision, not a technical one, and
/// needs sign-off before being presented as an authoritative ranking (see Phase 10 plan open risk #6).
/// </summary>
public static class DriverScoreCalculator
{
    private const int OverspeedPenaltyPerEvent = 2;
    private const int MaxOverspeedPenalty = 30;

    private const int HarshEventPenaltyPerEvent = 3;
    private const int MaxHarshPenalty = 20;

    private const decimal IdlePercentPenaltyThreshold = 30m;
    private const int MaxIdlePenalty = 20;

    private const decimal NightDrivingPenaltyThreshold = 20m;
    private const int MaxNightPenalty = 10;

    public static int ComputeScore(DriverScoreFactorsDto factors, decimal idlePercent)
    {
        var score = 100;

        score -= Math.Min(MaxOverspeedPenalty, factors.OverspeedCount * OverspeedPenaltyPerEvent);
        score -= Math.Min(MaxHarshPenalty, factors.HarshEventCount * HarshEventPenaltyPerEvent);

        if (idlePercent > IdlePercentPenaltyThreshold)
            score -= Math.Min(MaxIdlePenalty, (int)Math.Round(idlePercent - IdlePercentPenaltyThreshold));

        if (factors.NightDrivingPercent > NightDrivingPenaltyThreshold)
            score -= Math.Min(MaxNightPenalty, (int)Math.Round((factors.NightDrivingPercent - NightDrivingPenaltyThreshold) / 2));

        return Math.Clamp(score, 0, 100);
    }

    public static string RatingFor(int score) => score switch
    {
        >= 90 => "Excellent",
        >= 75 => "Good",
        >= 60 => "Fair",
        _ => "Poor"
    };

    /// <summary>Safety sub-score from overspeed + harsh events (100 = clean).</summary>
    public static int ComputeSafetyScore(DriverScoreFactorsDto factors)
    {
        var penalty = Math.Min(MaxOverspeedPenalty, factors.OverspeedCount * OverspeedPenaltyPerEvent)
                      + Math.Min(MaxHarshPenalty, factors.HarshEventCount * HarshEventPenaltyPerEvent);
        return Math.Clamp(100 - penalty, 0, 100);
    }

    /// <summary>Fuel efficiency from km/L when fuel logs exist for the period; neutral 70 when unknown.</summary>
    public static int ComputeFuelEfficiencyScore(decimal distanceKm, decimal? fuelLiters)
    {
        if (fuelLiters is null or <= 0 || distanceKm <= 0)
            return 70;

        var kmPerLiter = distanceKm / fuelLiters.Value;
        return kmPerLiter switch
        {
            >= 12m => 100,
            >= 10m => 90,
            >= 8m => 80,
            >= 6m => 65,
            >= 4m => 50,
            _ => 35
        };
    }

    /// <summary>On-time delivery from completed trips with planned end times.</summary>
    public static int ComputeOnTimeScore(int completedWithPlan, int onTimeCount)
    {
        if (completedWithPlan <= 0)
            return 70;
        var pct = onTimeCount * 100m / completedWithPlan;
        return (int)Math.Clamp(Math.Round(pct), 0, 100);
    }

    public static bool NeedsTrainingFlag(int overallScore, int safetyScore, int onTimeScore)
        => overallScore < 60 || safetyScore < 55 || onTimeScore < 50;

    public static bool RewardEligibleFlag(int overallScore, int rank, int totalDrivers)
        => overallScore >= 85 && rank > 0 && totalDrivers > 0 && rank <= Math.Max(1, (int)Math.Ceiling(totalDrivers * 0.2m));
}
