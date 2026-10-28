using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Pure ranking enricher — no SQL. Callers supply on-time counts and peer medians.
/// </summary>
public static class DriverPerformanceEnricher
{
    public static List<DriverScoreDto> EnrichAndRank(
        IReadOnlyList<DriverScoreDto> ranked,
        IReadOnlyDictionary<int, (int Completed, int OnTime)> onTimeByDriver,
        DriverScoreWeightsDto weights,
        IReadOnlyDictionary<int, string?> primaryCategoryByDriver,
        IReadOnlyDictionary<string, decimal> peerMedianKmPerLiterByCategory)
    {
        if (ranked.Count == 0)
            return [];

        var w = DriverScoreCalculator.NormalizeWeights(
            weights.SafetyWeight, weights.FuelWeight, weights.OnTimeWeight);

        var draft = new List<(DriverScoreDto Row, int Safety, int Fuel, int OnTime, int Behavior, decimal? KmPerL, decimal? PeerMedian, bool UsedPeer, string? Category)>(ranked.Count);

        foreach (var row in ranked)
        {
            var (completed, onTime) = onTimeByDriver.GetValueOrDefault(row.DriverId);
            var safety = DriverScoreCalculator.ComputeSafetyScore(row.Factors);
            var category = primaryCategoryByDriver.GetValueOrDefault(row.DriverId);
            decimal? peerMedian = null;
            var usedPeer = false;
            if (!string.IsNullOrWhiteSpace(category)
                && peerMedianKmPerLiterByCategory.TryGetValue(category, out var median))
            {
                peerMedian = median;
                usedPeer = true;
            }

            var fuel = DriverScoreCalculator.ComputeFuelEfficiencyScore(
                row.Factors.DistanceKm, row.Factors.FuelLiters, peerMedian);

            var onTimeScore = DriverScoreCalculator.ComputeOnTimeScore(completed, onTime);
            var behavior = row.SecondaryBehaviorScore;

            decimal? kmPerL = null;
            if (row.Factors.FuelLiters is > 0 && row.Factors.DistanceKm > 0)
                kmPerL = Math.Round(row.Factors.DistanceKm / row.Factors.FuelLiters.Value, 2);

            draft.Add((row, safety, fuel, onTimeScore, behavior, kmPerL, peerMedian, usedPeer, category));
        }

        var withOverall = draft
            .Select(d =>
            {
                var overall = DriverScoreCalculator.ComputeOverallScore(d.Safety, d.Fuel, d.OnTime, w);
                return (d.Row, d.Safety, d.Fuel, d.OnTime, d.Behavior, d.KmPerL, d.PeerMedian, d.UsedPeer, d.Category, Overall: overall);
            })
            .OrderByDescending(x => x.Overall)
            .ThenBy(x => x.Row.DriverName)
            .ToList();

        var total = withOverall.Count;
        var enriched = new List<DriverScoreDto>(total);

        for (var i = 0; i < withOverall.Count; i++)
        {
            var item = withOverall[i];
            var rank = i + 1;
            var needsTraining = DriverScoreCalculator.NeedsTrainingFlag(item.Overall, item.Safety, item.OnTime);
            var reward = DriverScoreCalculator.RewardEligibleFlag(item.Overall, rank, total);
            var explanation = DriverScoreCalculator.BuildChangeExplanation(
                previousOverall: null,
                overall: item.Overall,
                safety: item.Safety,
                fuel: item.Fuel,
                onTime: item.OnTime,
                factors: item.Row.Factors,
                fuelUsedPeerBand: item.UsedPeer,
                kmPerLiter: item.KmPerL,
                weights: w);

            enriched.Add(item.Row with
            {
                Score = item.Overall,
                Rating = DriverScoreCalculator.RatingFor(item.Overall),
                Rank = rank,
                TotalDrivers = total,
                SafetyScore = item.Safety,
                FuelEfficiencyScore = item.Fuel,
                OnTimeScore = item.OnTime,
                NeedsTraining = needsTraining,
                RewardEligible = reward,
                SecondaryBehaviorScore = item.Behavior,
                ScoreExplanation = explanation,
                PrimaryVehicleCategory = item.Category,
                KmPerLiter = item.KmPerL,
                PeerMedianKmPerLiter = item.PeerMedian,
                FuelUsedPeerBand = item.UsedPeer,
                Weights = w
            });
        }

        return enriched;
    }
}
