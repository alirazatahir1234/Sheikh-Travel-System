using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetPositionHeatmapQuery(int[] VehicleIds, DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<List<HeatmapPointDto>>>;

/// <summary>
/// Vehicle-selection-bounded by design, not fleet-wide — GpsPositions has no TenantId column and is
/// indexed only on (VehicleId, RecordedAt), so a fleet-wide heatmap would mean either a full table
/// scan or an unbounded per-vehicle fan-out against the highest-write table in the schema. Capped at
/// MaxVehicles, each queried against the existing per-vehicle index exactly like Replay/History
/// already do it. 90-day retention (GpsSettings.PositionRetentionDays) is inherited, same as every
/// other position-based feature.
/// </summary>
public class GetPositionHeatmapQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetPositionHeatmapQuery, ApiResponse<List<HeatmapPointDto>>>
{
    private const int MaxVehicles = 15;
    private const double GridSize = 0.001; // ~111m at the equator

    public async Task<ApiResponse<List<HeatmapPointDto>>> Handle(GetPositionHeatmapQuery request, CancellationToken cancellationToken)
    {
        if (request.VehicleIds.Length == 0)
            return ApiResponse<List<HeatmapPointDto>>.FailResponse("Select at least one vehicle.");

        if (request.VehicleIds.Length > MaxVehicles)
            return ApiResponse<List<HeatmapPointDto>>.FailResponse($"Select at most {MaxVehicles} vehicles for the heatmap.");

        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var ownedVehicleIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Id FROM Vehicles WHERE TenantId = @TenantId AND IsDeleted = 0 AND Id IN @VehicleIds",
            new { TenantId = tenantId, VehicleIds = request.VehicleIds },
            cancellationToken: cancellationToken))).ToList();

        if (ownedVehicleIds.Count == 0)
            return ApiResponse<List<HeatmapPointDto>>.SuccessResponse([]);

        var positions = await connection.QueryAsync<(double Latitude, double Longitude)>(new CommandDefinition(
            """
            SELECT Latitude, Longitude FROM GpsPositions
            WHERE VehicleId IN @VehicleIds AND RecordedAt BETWEEN @FromDate AND @ToDate
            """,
            new { VehicleIds = ownedVehicleIds, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken));

        var binned = positions
            .GroupBy(p => (Lat: Math.Round(p.Latitude / GridSize) * GridSize, Lng: Math.Round(p.Longitude / GridSize) * GridSize))
            .Select(g => new HeatmapPointDto(g.Key.Lat, g.Key.Lng, g.Count()))
            .OrderByDescending(p => p.Count)
            .Take(5000)
            .ToList();

        return ApiResponse<List<HeatmapPointDto>>.SuccessResponse(binned);
    }
}

public record GetVehicleHealthScoreQuery(int? BranchId = null, int? DepartmentId = null)
    : IRequest<ApiResponse<List<GpsVehicleHealthDto>>>;

/// <summary>
/// Zero new signal logic — composes MaintenanceScheduleHelper.ComputeStatus() (reused verbatim,
/// same code the Maintenance dashboard already uses) with Vehicles.InsuranceExpiryDate and
/// GpsDevices.WarrantyStart/End, all of which already exist. VehicleMaintenanceSchedules only
/// carries Date/Mileage intervals (no EngineHours columns despite the helper supporting that axis),
/// so EngineHours is passed as null here — consistent with the table's actual schema, not a bug.
/// </summary>
public class GetVehicleHealthScoreQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleHealthScoreQuery, ApiResponse<List<GpsVehicleHealthDto>>>
{
    public async Task<ApiResponse<List<GpsVehicleHealthDto>>> Handle(GetVehicleHealthScoreQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var sql = """
            SELECT v.Id AS VehicleId, v.Name AS VehicleName, v.InsuranceExpiryDate, v.CurrentMileage,
                   d.LastBatteryLevel, d.LastRssi, d.WarrantyEnd AS TrackerWarrantyEnd
            FROM Vehicles v
            LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0
            """;

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId);

        if (request.BranchId.HasValue)
        {
            sql += " AND v.BranchId = @BranchId";
            parameters.Add("BranchId", request.BranchId.Value);
        }

        if (request.DepartmentId.HasValue)
        {
            sql += " AND v.DepartmentId = @DepartmentId";
            parameters.Add("DepartmentId", request.DepartmentId.Value);
        }

        var vehicles = (await connection.QueryAsync<(int VehicleId, string VehicleName, DateTime? InsuranceExpiryDate, decimal CurrentMileage, decimal? LastBatteryLevel, int? LastRssi, DateTime? TrackerWarrantyEnd)>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();

        if (vehicles.Count == 0)
            return ApiResponse<List<GpsVehicleHealthDto>>.SuccessResponse([]);

        var vehicleIds = vehicles.Select(v => v.VehicleId).ToList();
        var schedules = (await connection.QueryAsync<(int VehicleId, string IntervalType, DateTime? NextDueDate, decimal? NextDueMileage)>(
            new CommandDefinition(
                """
                SELECT VehicleId, IntervalType, NextDueDate, NextDueMileage
                FROM VehicleMaintenanceSchedules
                WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1 AND VehicleId IN @VehicleIds
                """,
                new { TenantId = tenantId, VehicleIds = vehicleIds },
                cancellationToken: cancellationToken))).ToList();

        var result = vehicles.Select(v =>
        {
            var vehicleSchedules = schedules.Where(s => s.VehicleId == v.VehicleId).ToList();
            var maintenanceStatus = vehicleSchedules.Count == 0
                ? "None"
                : vehicleSchedules
                    .Select(s => MaintenanceScheduleHelper.ComputeStatus(s.IntervalType, s.NextDueDate, s.NextDueMileage, null, v.CurrentMileage, null))
                    .OrderByDescending(SeverityRank)
                    .First();

            return new GpsVehicleHealthDto(
                v.VehicleId,
                v.VehicleName,
                v.LastBatteryLevel,
                v.LastRssi,
                maintenanceStatus,
                v.InsuranceExpiryDate,
                ExpiryStatusFor(v.InsuranceExpiryDate),
                v.TrackerWarrantyEnd,
                ExpiryStatusFor(v.TrackerWarrantyEnd));
        }).ToList();

        return ApiResponse<List<GpsVehicleHealthDto>>.SuccessResponse(result);
    }

    private static int SeverityRank(string status) => status switch
    {
        MaintenanceScheduleHelper.StatusOverdue => 3,
        MaintenanceScheduleHelper.StatusDueSoon => 2,
        _ => 1
    };

    private static string ExpiryStatusFor(DateTime? date)
    {
        if (!date.HasValue) return "Unknown";
        var days = (date.Value.Date - DateTime.UtcNow.Date).TotalDays;
        if (days < 0) return "Expired";
        if (days <= 30) return "ExpiringSoon";
        return "Valid";
    }
}

public record GetVehicleRankingQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null)
    : IRequest<ApiResponse<List<VehicleRankingDto>>>;

/// <summary>Extends the same Fuel+Maintenance per-vehicle cost join MaintenanceDashboardQueries.GetFuelSummaryAsync already uses, adding trip distance/speed for a cost/km ranking.</summary>
public class GetVehicleRankingQueryHandler(IDbConnectionFactory dbFactory, IMediator mediator, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleRankingQuery, ApiResponse<List<VehicleRankingDto>>>
{
    public async Task<ApiResponse<List<VehicleRankingDto>>> Handle(GetVehicleRankingQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, null, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
            return ApiResponse<List<VehicleRankingDto>>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");

        var trips = tripsResponse.Data.Items;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var branchClause = request.BranchId.HasValue ? " AND v.BranchId = @BranchId" : "";
        var departmentClause = request.DepartmentId.HasValue ? " AND v.DepartmentId = @DepartmentId" : "";

        var costs = (await connection.QueryAsync<(int VehicleId, decimal FuelCost, decimal MaintenanceCost)>(new CommandDefinition(
            $"""
            SELECT v.Id AS VehicleId,
                ISNULL((SELECT SUM(f.TotalCost) FROM FuelLogs f WHERE f.VehicleId = v.Id AND f.IsDeleted = 0
                    AND f.FuelDate >= @FromDate AND f.FuelDate < @ToDate), 0) AS FuelCost,
                ISNULL((SELECT SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)) FROM Maintenance m
                    WHERE m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.MaintenanceDate >= @FromDate AND m.MaintenanceDate < @ToDate), 0) AS MaintenanceCost
            FROM Vehicles v
            WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause} {departmentClause}
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate, request.BranchId, request.DepartmentId },
            cancellationToken: cancellationToken))).ToDictionary(c => c.VehicleId);

        var ranking = trips
            .GroupBy(t => new { t.VehicleId, t.VehicleName })
            .Select(g =>
            {
                var distance = (decimal)g.Sum(t => t.DistanceKm);
                var costEntry = costs.GetValueOrDefault(g.Key.VehicleId);
                var totalCost = costEntry.FuelCost + costEntry.MaintenanceCost;
                return new VehicleRankingDto(
                    g.Key.VehicleId,
                    g.Key.VehicleName ?? $"Vehicle #{g.Key.VehicleId}",
                    Math.Round(distance, 2),
                    g.Count(),
                    Math.Round(g.Average(t => t.AvgSpeedKmh), 1),
                    costEntry.FuelCost,
                    costEntry.MaintenanceCost,
                    distance > 0 ? Math.Round(totalCost / distance, 2) : null);
            })
            .OrderByDescending(v => v.DistanceKm)
            .ToList();

        return ApiResponse<List<VehicleRankingDto>>.SuccessResponse(ranking);
    }
}
