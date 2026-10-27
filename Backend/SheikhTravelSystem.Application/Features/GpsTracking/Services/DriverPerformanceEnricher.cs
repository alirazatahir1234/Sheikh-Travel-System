using Dapper;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class DriverPerformanceEnricher
{
    private const int OnTimeGraceMinutes = 15;

    public static async Task<List<DriverScoreDto>> EnrichAndRankAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        IReadOnlyList<DriverScoreDto> ranked,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        if (ranked.Count == 0)
            return [];

        var driverIds = ranked.Select(r => r.DriverId).Distinct().ToList();
        var onTimeRows = await connection.QueryAsync<(int DriverId, int Completed, int OnTime)>(
            new CommandDefinition(
                """
                SELECT t.DriverId,
                       COUNT(*) AS Completed,
                       SUM(CASE
                             WHEN t.ActualEnd IS NOT NULL AND t.PlannedEnd IS NOT NULL
                              AND t.ActualEnd <= DATEADD(MINUTE, @Grace, t.PlannedEnd)
                             THEN 1 ELSE 0 END) AS OnTime
                FROM Trips t
                WHERE t.TenantId = @TenantId AND t.IsDeleted = 0
                  AND t.DriverId IN @DriverIds
                  AND t.Status = @Completed
                  AND t.ActualEnd BETWEEN @FromDate AND @ToDate
                GROUP BY t.DriverId
                """,
                new
                {
                    TenantId = tenantId,
                    DriverIds = driverIds,
                    Completed = (int)TripStatus.Completed,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Grace = OnTimeGraceMinutes
                },
                cancellationToken: cancellationToken));

        var onTimeMap = onTimeRows.ToDictionary(r => r.DriverId, r => (r.Completed, r.OnTime));
        var total = ranked.Count;
        var enriched = new List<DriverScoreDto>(total);

        for (var i = 0; i < ranked.Count; i++)
        {
            var row = ranked[i];
            var rank = i + 1;
            var (completed, onTime) = onTimeMap.GetValueOrDefault(row.DriverId);
            var safety = DriverScoreCalculator.ComputeSafetyScore(row.Factors);
            var fuel = DriverScoreCalculator.ComputeFuelEfficiencyScore(row.Factors.DistanceKm, row.Factors.FuelLiters);
            var onTimeScore = DriverScoreCalculator.ComputeOnTimeScore(completed, onTime);
            var needsTraining = DriverScoreCalculator.NeedsTrainingFlag(row.Score, safety, onTimeScore);
            var reward = DriverScoreCalculator.RewardEligibleFlag(row.Score, rank, total);

            enriched.Add(row with
            {
                Rank = rank,
                TotalDrivers = total,
                SafetyScore = safety,
                FuelEfficiencyScore = fuel,
                OnTimeScore = onTimeScore,
                NeedsTraining = needsTraining,
                RewardEligible = reward
            });
        }

        return enriched;
    }
}
