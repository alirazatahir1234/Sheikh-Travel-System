using System.Text.Json;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Trips.SmartAssignment;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class TripSmartAssignmentRepository(IDbConnectionFactory dbFactory) : ITripSmartAssignmentRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<TripSmartAssignmentTripContext?> GetTripContextAsync(
        int tenantId, int tripId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TripSmartAssignmentTripContext>(new CommandDefinition(
            """
            SELECT t.Id AS TripId, t.TripNumber, t.Status, t.PlannedStart,
                   t.PickupLatitude, t.PickupLongitude, t.DestinationLatitude, t.DestinationLongitude,
                   t.RouteId, t.DriverId AS CurrentDriverId, t.VehicleId AS CurrentVehicleId
            FROM Trips t
            WHERE t.Id = @TripId AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """,
            new { TripId = tripId, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TripAssignmentCandidateRow>> GetCandidatesAsync(
        int tenantId,
        IReadOnlyCollection<int> excludedDriverIds,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<TripAssignmentCandidateRow>(new CommandDefinition(
            """
            SELECT
                d.Id AS DriverId,
                d.FullName AS DriverName,
                d.UserId,
                v.Id AS VehicleId,
                v.VehicleName,
                vcl.Latitude,
                vcl.Longitude,
                vcl.FuelLevel AS FuelLevelPercent,
                COALESCE(hours.HoursWorked, 0) AS HoursWorkedToday,
                d.Rating,
                d.IsActive,
                d.Status,
                d.VerificationStatus,
                d.LicenseExpiryDate,
                v.FuelType AS VehicleFuelType,
                v.ElectricRangeKm
            FROM Drivers d
            OUTER APPLY (
                SELECT TOP 1 ah.VehicleId
                FROM AssignmentHistory ah
                WHERE ah.DriverId = d.Id AND ah.TenantId = @TenantId AND ah.IsDeleted = 0 AND ah.Status = N'Active'
                ORDER BY ah.StartAt DESC
            ) activeVeh
            LEFT JOIN Vehicles v ON v.Id = activeVeh.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            OUTER APPLY (
                SELECT CAST(DATEDIFF(MINUTE,
                    COALESCE(da.CheckInAt, da.RecordedAt),
                    COALESCE(da.CheckOutAt, SYSUTCDATETIME())) AS DECIMAL(10,2)) / 60.0 AS HoursWorked
                FROM DriverAttendance da
                WHERE da.DriverId = d.Id AND da.TenantId = @TenantId AND da.IsDeleted = 0
                  AND da.AttendanceDate = CAST(SYSUTCDATETIME() AS DATE)
            ) hours
            WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND d.IsActive = 1
              AND d.Status IN (@Available, @OnTrip)
              AND (@ExcludedCount = 0 OR d.Id NOT IN @ExcludedDriverIds)
            """,
            new
            {
                TenantId = tenantId,
                ExcludedDriverIds = excludedDriverIds.Count > 0 ? excludedDriverIds : [-1],
                ExcludedCount = excludedDriverIds.Count,
                Available = (int)DriverStatus.Available,
                OnTrip = (int)DriverStatus.OnTrip
            },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<long> InsertDecisionAsync(
        int tenantId,
        TripSmartAssignmentDecisionWrite decision,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO TripSmartAssignmentDecisions
                (TenantId, TripId, DriverId, VehicleId, Rank, TotalScore, ScoreBreakdownJson,
                 RouteId, DistanceKm, EtaMinutes, TrafficSource, Outcome, RejectionReason, CreatedBy, CreatedAtUtc)
            OUTPUT INSERTED.Id
            VALUES
                (@TenantId, @TripId, @DriverId, @VehicleId, @Rank, @TotalScore, @ScoreBreakdownJson,
                 @RouteId, @DistanceKm, @EtaMinutes, @TrafficSource, @Outcome, @RejectionReason, @CreatedBy, SYSUTCDATETIME())
            """,
            new
            {
                TenantId = tenantId,
                decision.TripId,
                decision.DriverId,
                decision.VehicleId,
                decision.Rank,
                decision.TotalScore,
                decision.ScoreBreakdownJson,
                decision.RouteId,
                decision.DistanceKm,
                decision.EtaMinutes,
                decision.TrafficSource,
                decision.Outcome,
                decision.RejectionReason,
                decision.CreatedBy
            },
            cancellationToken: cancellationToken));
    }

    public async Task MarkPriorProposedSupersededAsync(int tenantId, int tripId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE TripSmartAssignmentDecisions
            SET Outcome = N'Superseded'
            WHERE TenantId = @TenantId AND TripId = @TripId AND Outcome = N'Proposed'
            """,
            new { TenantId = tenantId, TripId = tripId },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateDecisionOutcomeAsync(
        int tenantId,
        long decisionId,
        string outcome,
        string? rejectionReason,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE TripSmartAssignmentDecisions
            SET Outcome = @Outcome, RejectionReason = COALESCE(@RejectionReason, RejectionReason)
            WHERE Id = @Id AND TenantId = @TenantId
            """,
            new { Id = decisionId, TenantId = tenantId, Outcome = outcome, RejectionReason = rejectionReason },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TripSmartAssignmentDecisionDto>> GetDecisionsForTripAsync(
        int tenantId, int tripId, CancellationToken cancellationToken = default)
        => await QueryDecisionsAsync(
            """
            SELECT d.Id, d.TripId, t.TripNumber, d.DriverId, dr.FullName AS DriverName,
                   d.VehicleId, v.VehicleName, d.Rank, d.TotalScore, d.ScoreBreakdownJson,
                   d.DistanceKm, d.EtaMinutes, d.TrafficSource, d.Outcome, d.RejectionReason,
                   d.CreatedBy, d.CreatedAtUtc
            FROM TripSmartAssignmentDecisions d
            INNER JOIN Trips t ON t.Id = d.TripId AND t.TenantId = d.TenantId
            LEFT JOIN Drivers dr ON dr.Id = d.DriverId
            LEFT JOIN Vehicles v ON v.Id = d.VehicleId
            WHERE d.TenantId = @TenantId AND d.TripId = @TripId
            ORDER BY d.CreatedAtUtc DESC, d.Rank ASC
            """,
            new { TenantId = tenantId, TripId = tripId },
            cancellationToken);

    public async Task<IReadOnlyList<TripSmartAssignmentDecisionDto>> GetRecentDecisionsAsync(
        int tenantId, int limit, CancellationToken cancellationToken = default)
        => await QueryDecisionsAsync(
            """
            SELECT TOP (@Limit) d.Id, d.TripId, t.TripNumber, d.DriverId, dr.FullName AS DriverName,
                   d.VehicleId, v.VehicleName, d.Rank, d.TotalScore, d.ScoreBreakdownJson,
                   d.DistanceKm, d.EtaMinutes, d.TrafficSource, d.Outcome, d.RejectionReason,
                   d.CreatedBy, d.CreatedAtUtc
            FROM TripSmartAssignmentDecisions d
            INNER JOIN Trips t ON t.Id = d.TripId AND t.TenantId = d.TenantId
            LEFT JOIN Drivers dr ON dr.Id = d.DriverId
            LEFT JOIN Vehicles v ON v.Id = d.VehicleId
            WHERE d.TenantId = @TenantId
            ORDER BY d.CreatedAtUtc DESC
            """,
            new { TenantId = tenantId, Limit = Math.Clamp(limit, 1, 200) },
            cancellationToken);

    public async Task<IReadOnlyList<int>> GetRejectedDriverIdsAsync(
        int tenantId, int tripId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var ids = await connection.QueryAsync<int>(new CommandDefinition(
            """
            SELECT DISTINCT DriverId FROM TripSmartAssignmentDecisions
            WHERE TenantId = @TenantId AND TripId = @TripId AND Outcome = N'Rejected'
            """,
            new { TenantId = tenantId, TripId = tripId },
            cancellationToken: cancellationToken));
        return ids.ToList();
    }

    public async Task<TripSmartAssignmentDecisionDto?> GetLatestActiveDecisionAsync(
        int tenantId, int tripId, int driverId, CancellationToken cancellationToken = default)
    {
        var list = await QueryDecisionsAsync(
            """
            SELECT TOP 1 d.Id, d.TripId, t.TripNumber, d.DriverId, dr.FullName AS DriverName,
                   d.VehicleId, v.VehicleName, d.Rank, d.TotalScore, d.ScoreBreakdownJson,
                   d.DistanceKm, d.EtaMinutes, d.TrafficSource, d.Outcome, d.RejectionReason,
                   d.CreatedBy, d.CreatedAtUtc
            FROM TripSmartAssignmentDecisions d
            INNER JOIN Trips t ON t.Id = d.TripId AND t.TenantId = d.TenantId
            LEFT JOIN Drivers dr ON dr.Id = d.DriverId
            LEFT JOIN Vehicles v ON v.Id = d.VehicleId
            WHERE d.TenantId = @TenantId AND d.TripId = @TripId AND d.DriverId = @DriverId
              AND d.Outcome IN (N'Proposed', N'Accepted', N'Overridden')
            ORDER BY d.CreatedAtUtc DESC
            """,
            new { TenantId = tenantId, TripId = tripId, DriverId = driverId },
            cancellationToken);
        return list.FirstOrDefault();
    }

    private async Task<IReadOnlyList<TripSmartAssignmentDecisionDto>> QueryDecisionsAsync(
        string sql, object param, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<DecisionRow>(new CommandDefinition(sql, param, cancellationToken: cancellationToken));
        return rows.Select(MapDecision).ToList();
    }

    private static TripSmartAssignmentDecisionDto MapDecision(DecisionRow row)
    {
        IReadOnlyList<TripAssignmentScoreFactorDto> factors = [];
        if (!string.IsNullOrWhiteSpace(row.ScoreBreakdownJson))
        {
            try
            {
                factors = JsonSerializer.Deserialize<List<TripAssignmentScoreFactorDto>>(row.ScoreBreakdownJson, JsonOpts) ?? [];
            }
            catch
            {
                factors = [];
            }
        }

        return new TripSmartAssignmentDecisionDto(
            row.Id,
            row.TripId,
            row.TripNumber,
            row.DriverId,
            row.DriverName,
            row.VehicleId,
            row.VehicleName,
            row.Rank,
            row.TotalScore,
            factors,
            row.DistanceKm,
            row.EtaMinutes,
            row.TrafficSource,
            row.Outcome,
            row.RejectionReason,
            row.CreatedBy,
            row.CreatedAtUtc);
    }

    private sealed class DecisionRow
    {
        public long Id { get; init; }
        public int TripId { get; init; }
        public string? TripNumber { get; init; }
        public int DriverId { get; init; }
        public string? DriverName { get; init; }
        public int? VehicleId { get; init; }
        public string? VehicleName { get; init; }
        public int Rank { get; init; }
        public decimal TotalScore { get; init; }
        public string ScoreBreakdownJson { get; init; } = "";
        public decimal? DistanceKm { get; init; }
        public int? EtaMinutes { get; init; }
        public string TrafficSource { get; init; } = "";
        public string Outcome { get; init; } = "";
        public string? RejectionReason { get; init; }
        public string? CreatedBy { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}
