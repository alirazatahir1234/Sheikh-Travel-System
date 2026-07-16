using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Pure scoring calculator — realistically scoped to signals reliably available fleet-wide
/// (overspeed from GpsAlertEvents.DriverId, distance/night-driving from GpsTrips via an
/// AssignmentHistory time-window join) plus Traccar-only idle/harsh-event factors when available.
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
}
