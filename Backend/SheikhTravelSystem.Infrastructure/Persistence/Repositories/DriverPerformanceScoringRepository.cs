using System.Text.Json;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public class DriverPerformanceScoringRepository(IDbConnectionFactory dbFactory) : IDriverPerformanceScoringRepository
{
    private const int OnTimeGraceMinutes = 15;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<DriverScoreWeightsDto> GetOrCreateWeightsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM DriverPerformanceScoreWeights WHERE TenantId = @TenantId)
                INSERT INTO DriverPerformanceScoreWeights (TenantId, SafetyWeight, FuelWeight, OnTimeWeight)
                VALUES (@TenantId, @Safety, @Fuel, @OnTime);
            """,
            new
            {
                TenantId = tenantId,
                Safety = DriverScoreCalculator.DefaultSafetyWeight,
                Fuel = DriverScoreCalculator.DefaultFuelWeight,
                OnTime = DriverScoreCalculator.DefaultOnTimeWeight
            },
            cancellationToken: cancellationToken));

        return await connection.QuerySingleAsync<DriverScoreWeightsDto>(new CommandDefinition("""
            SELECT SafetyWeight, FuelWeight, OnTimeWeight
            FROM DriverPerformanceScoreWeights WHERE TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<DriverScoreWeightsDto> UpsertWeightsAsync(
        int tenantId,
        UpsertDriverScoreWeightsRequest body,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var normalized = DriverScoreCalculator.NormalizeWeights(
            body.SafetyWeight, body.FuelWeight, body.OnTimeWeight);

        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE DriverPerformanceScoreWeights AS t
            USING (SELECT @TenantId AS TenantId) AS s ON t.TenantId = s.TenantId
            WHEN MATCHED THEN UPDATE SET
                SafetyWeight = @SafetyWeight,
                FuelWeight = @FuelWeight,
                OnTimeWeight = @OnTimeWeight,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT (TenantId, SafetyWeight, FuelWeight, OnTimeWeight, UpdatedBy)
                VALUES (@TenantId, @SafetyWeight, @FuelWeight, @OnTimeWeight, @UpdatedBy);
            """,
            new
            {
                TenantId = tenantId,
                normalized.SafetyWeight,
                normalized.FuelWeight,
                normalized.OnTimeWeight,
                UpdatedBy = updatedBy
            },
            cancellationToken: cancellationToken));

        return normalized;
    }

    public async Task RecordScoreHistoryAsync(
        int tenantId,
        IReadOnlyList<DriverScoreDto> scores,
        DateTime periodFrom,
        DateTime periodTo,
        CancellationToken cancellationToken = default)
    {
        if (scores.Count == 0)
            return;

        using var connection = dbFactory.CreateConnection();

        foreach (var score in scores)
        {
            var previous = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("""
                SELECT TOP 1 OverallScore
                FROM DriverPerformanceScoreHistory
                WHERE TenantId = @TenantId AND DriverId = @DriverId
                  AND PeriodFrom = @PeriodFrom AND PeriodTo = @PeriodTo
                ORDER BY ComputedAt DESC
                """,
                new
                {
                    TenantId = tenantId,
                    score.DriverId,
                    PeriodFrom = periodFrom,
                    PeriodTo = periodTo
                },
                cancellationToken: cancellationToken));

            if (previous == score.Score)
                continue;

            var explanation = DriverScoreCalculator.BuildChangeExplanation(
                previous,
                score.Score,
                score.SafetyScore,
                score.FuelEfficiencyScore,
                score.OnTimeScore,
                score.Factors,
                score.FuelUsedPeerBand,
                score.KmPerLiter,
                score.Weights ?? DriverScoreCalculator.DefaultWeights());

            var weightsJson = score.Weights is null
                ? null
                : JsonSerializer.Serialize(score.Weights, JsonOpts);
            var factorsJson = JsonSerializer.Serialize(score.Factors, JsonOpts);

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO DriverPerformanceScoreHistory (
                    TenantId, DriverId, PeriodFrom, PeriodTo,
                    OverallScore, SafetyScore, FuelScore, OnTimeScore, SecondaryBehaviorScore,
                    PreviousOverallScore, Delta, Explanation, WeightsJson, FactorsJson,
                    PrimaryVehicleCategory, KmPerLiter, PeerMedianKmPerLiter, FuelUsedPeerBand, IsPartial)
                VALUES (
                    @TenantId, @DriverId, @PeriodFrom, @PeriodTo,
                    @OverallScore, @SafetyScore, @FuelScore, @OnTimeScore, @SecondaryBehaviorScore,
                    @PreviousOverallScore, @Delta, @Explanation, @WeightsJson, @FactorsJson,
                    @PrimaryVehicleCategory, @KmPerLiter, @PeerMedianKmPerLiter, @FuelUsedPeerBand, @IsPartial);
                """,
                new
                {
                    TenantId = tenantId,
                    score.DriverId,
                    PeriodFrom = periodFrom,
                    PeriodTo = periodTo,
                    OverallScore = score.Score,
                    SafetyScore = score.SafetyScore,
                    FuelScore = score.FuelEfficiencyScore,
                    OnTimeScore = score.OnTimeScore,
                    SecondaryBehaviorScore = score.SecondaryBehaviorScore,
                    PreviousOverallScore = previous,
                    Delta = previous is null ? 0 : score.Score - previous.Value,
                    Explanation = Truncate(explanation, 1000),
                    WeightsJson = weightsJson,
                    FactorsJson = factorsJson,
                    PrimaryVehicleCategory = score.PrimaryVehicleCategory,
                    KmPerLiter = score.KmPerLiter,
                    PeerMedianKmPerLiter = score.PeerMedianKmPerLiter,
                    FuelUsedPeerBand = score.FuelUsedPeerBand,
                    IsPartial = score.IsPartial
                },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<DriverScoreHistoryDto>> GetHistoryAsync(
        int tenantId,
        int driverId,
        DateTime? from,
        DateTime? to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var take = Math.Clamp(limit, 1, 200);
        var rows = await connection.QueryAsync<DriverScoreHistoryDto>(new CommandDefinition("""
            SELECT TOP (@Limit)
                Id, DriverId, PeriodFrom, PeriodTo,
                OverallScore, SafetyScore, FuelScore, OnTimeScore,
                PreviousOverallScore, Delta, Explanation, IsPartial, ComputedAt
            FROM DriverPerformanceScoreHistory
            WHERE TenantId = @TenantId AND DriverId = @DriverId
              AND (@From IS NULL OR ComputedAt >= @From)
              AND (@To IS NULL OR ComputedAt <= @To)
            ORDER BY ComputedAt DESC
            """,
            new { TenantId = tenantId, DriverId = driverId, From = from, To = to, Limit = take },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<DriverScoreSupportingEventDto>> GetSupportingEventsAsync(
        int tenantId,
        int driverId,
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var take = Math.Clamp(limit, 1, 200);
        var rows = await connection.QueryAsync<DriverScoreSupportingEventDto>(new CommandDefinition("""
            SELECT TOP (@Limit)
                e.Id,
                e.EventType,
                e.Timestamp,
                CONCAT(e.EventType, CASE WHEN e.Severity IS NOT NULL THEN ' · ' + e.Severity ELSE '' END) AS Description,
                e.VehicleId
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            WHERE e.IsDeleted = 0
              AND e.DriverId = @DriverId
              AND e.Timestamp BETWEEN @From AND @To
              AND e.EventType IN ('overspeed', 'speed_exceeded', 'harsh_braking', 'harsh_acceleration')
            ORDER BY e.Timestamp DESC
            """,
            new { TenantId = tenantId, DriverId = driverId, From = from, To = to, Limit = take },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyDictionary<int, (int Completed, int OnTime)>> GetOnTimeCountsAsync(
        int tenantId,
        IReadOnlyList<int> driverIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        if (driverIds.Count == 0)
            return new Dictionary<int, (int, int)>();

        using var connection = dbFactory.CreateConnection();
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

        return onTimeRows.ToDictionary(r => r.DriverId, r => (r.Completed, r.OnTime));
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..(max - 1)] + "…";
}
