using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Pure rule-based driver performance scoring (Module 1).
/// Canonical Overall = weighted Safety + Fuel + On-time (tenant-configurable weights).
/// SecondaryBehaviorScore keeps the legacy overspeed/harsh/idle/night penalty for coaching only.
/// Seatbelt is absent — no sensor exists in this codebase.
/// </summary>
public static class DriverScoreCalculator
{
    public const decimal DefaultSafetyWeight = 40m;
    public const decimal DefaultFuelWeight = 30m;
    public const decimal DefaultOnTimeWeight = 30m;

    /// <summary>Neutral sub-score when a dimension has insufficient reliable data (no unfair penalty).</summary>
    public const int NeutralInsufficientDataScore = 70;

    private const int OverspeedPenaltyPerEvent = 2;
    private const int MaxOverspeedPenalty = 30;

    private const int HarshEventPenaltyPerEvent = 3;
    private const int MaxHarshPenalty = 20;

    private const decimal IdlePercentPenaltyThreshold = 30m;
    private const int MaxIdlePenalty = 20;

    private const decimal NightDrivingPenaltyThreshold = 20m;
    private const int MaxNightPenalty = 10;

    public static DriverScoreWeightsDto DefaultWeights() =>
        new(DefaultSafetyWeight, DefaultFuelWeight, DefaultOnTimeWeight);

    public static DriverScoreWeightsDto NormalizeWeights(decimal safety, decimal fuel, decimal onTime)
    {
        var s = Math.Max(0m, safety);
        var f = Math.Max(0m, fuel);
        var o = Math.Max(0m, onTime);
        var sum = s + f + o;
        if (sum <= 0)
            return DefaultWeights();
        return new DriverScoreWeightsDto(s, f, o);
    }

    /// <summary>
    /// Legacy driving-behavior score (overspeed/harsh/idle/night). Optional secondary signal only —
    /// not the canonical Overall.
    /// </summary>
    public static int ComputeBehaviorScore(DriverScoreFactorsDto factors, decimal idlePercent)
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

    /// <summary>Backward-compatible alias — prefer <see cref="ComputeBehaviorScore"/>.</summary>
    public static int ComputeScore(DriverScoreFactorsDto factors, decimal idlePercent)
        => ComputeBehaviorScore(factors, idlePercent);

    /// <summary>
    /// Canonical overall: weighted blend of Safety, Fuel, On-time.
    /// Weights are relative (need not sum to 100); zero-sum falls back to defaults.
    /// </summary>
    public static int ComputeOverallScore(
        int safetyScore,
        int fuelEfficiencyScore,
        int onTimeScore,
        DriverScoreWeightsDto? weights = null)
    {
        var w = weights is null
            ? DefaultWeights()
            : NormalizeWeights(weights.SafetyWeight, weights.FuelWeight, weights.OnTimeWeight);
        var sum = w.SafetyWeight + w.FuelWeight + w.OnTimeWeight;
        var blended = (safetyScore * w.SafetyWeight
                       + fuelEfficiencyScore * w.FuelWeight
                       + onTimeScore * w.OnTimeWeight) / sum;
        return (int)Math.Clamp(Math.Round(blended), 0, 100);
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

    /// <summary>
    /// Fuel efficiency. When <paramref name="peerMedianKmPerLiter"/> is available for the same
    /// vehicle category, score relative to peers; otherwise absolute km/L bands.
    /// Neutral 70 when fuel or distance is missing.
    /// </summary>
    public static int ComputeFuelEfficiencyScore(
        decimal distanceKm,
        decimal? fuelLiters,
        decimal? peerMedianKmPerLiter = null)
    {
        if (fuelLiters is null or <= 0 || distanceKm <= 0)
            return NeutralInsufficientDataScore;

        var kmPerLiter = distanceKm / fuelLiters.Value;

        if (peerMedianKmPerLiter is > 0)
        {
            var ratio = kmPerLiter / peerMedianKmPerLiter.Value;
            return ratio switch
            {
                >= 1.15m => 100,
                >= 1.05m => 90,
                >= 0.95m => 80,
                >= 0.85m => 65,
                >= 0.75m => 50,
                _ => 35
            };
        }

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
            return NeutralInsufficientDataScore;
        var pct = onTimeCount * 100m / completedWithPlan;
        return (int)Math.Clamp(Math.Round(pct), 0, 100);
    }

    public static bool NeedsTrainingFlag(int overallScore, int safetyScore, int onTimeScore)
        => overallScore < 60 || safetyScore < 55 || onTimeScore < 50;

    public static bool RewardEligibleFlag(int overallScore, int rank, int totalDrivers)
        => overallScore >= 85 && rank > 0 && totalDrivers > 0 && rank <= Math.Max(1, (int)Math.Ceiling(totalDrivers * 0.2m));

    public static decimal? Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
            return null;
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        if (sorted.Count % 2 == 1)
            return sorted[mid];
        return Math.Round((sorted[mid - 1] + sorted[mid]) / 2m, 3);
    }

    public static string BuildChangeExplanation(
        int? previousOverall,
        int overall,
        int safety,
        int fuel,
        int onTime,
        DriverScoreFactorsDto factors,
        bool fuelUsedPeerBand,
        decimal? kmPerLiter,
        DriverScoreWeightsDto weights)
    {
        var w = NormalizeWeights(weights.SafetyWeight, weights.FuelWeight, weights.OnTimeWeight);
        var parts = new List<string>
        {
            previousOverall is null
                ? $"Initial overall {overall}"
                : $"Overall {previousOverall}→{overall} ({DeltaText(overall - previousOverall.Value)})",
            $"Safety {safety} (overspeed {factors.OverspeedCount}, harsh {factors.HarshEventCount})",
            fuelUsedPeerBand && kmPerLiter is not null
                ? $"Fuel {fuel} (peer-band, {kmPerLiter:0.0} km/L)"
                : kmPerLiter is not null
                    ? $"Fuel {fuel} ({kmPerLiter:0.0} km/L absolute bands)"
                    : $"Fuel {fuel} (insufficient fuel data → neutral)",
            $"On-time {onTime}",
            $"Weights S/F/O {w.SafetyWeight:0}/{w.FuelWeight:0}/{w.OnTimeWeight:0}"
        };
        return string.Join("; ", parts);
    }

    private static string DeltaText(int delta) => delta >= 0 ? $"+{delta}" : delta.ToString();
}
