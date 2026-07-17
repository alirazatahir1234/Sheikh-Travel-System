using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetFuelAnalyticsQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null)
    : IRequest<ApiResponse<FuelAnalyticsDto>>;

/// <summary>
/// Fuel *cost* data already exists (FuelLogs, manual entries). Fuel *consumption efficiency* is
/// computed fresh here by cross-referencing FuelLogs.Liters with GpsTrips.DistanceKm over the same
/// range/filters — the first place in this codebase these two previously-unlinked data sources are
/// joined. GPS FuelLevel telemetry (Traccar) is a separate, still-unlinked signal — not used here.
/// </summary>
public class GetFuelAnalyticsQueryHandler(IDbConnectionFactory dbFactory, IMediator mediator, ITenantContext tenantContext)
    : IRequestHandler<GetFuelAnalyticsQuery, ApiResponse<FuelAnalyticsDto>>
{
    public async Task<ApiResponse<FuelAnalyticsDto>> Handle(GetFuelAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var sql = """
            SELECT f.VehicleId, v.Name AS VehicleName, f.Liters, f.TotalCost AS Cost, f.FuelDate
            FROM FuelLogs f
            INNER JOIN Vehicles v ON v.Id = f.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            WHERE f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.FuelDate BETWEEN @FromDate AND @ToDate
            """;

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId);
        parameters.Add("FromDate", fromDate);
        parameters.Add("ToDate", toDate);

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

        if (request.DriverId.HasValue)
        {
            sql += " AND f.DriverId = @DriverId";
            parameters.Add("DriverId", request.DriverId.Value);
        }

        var logs = (await connection.QueryAsync<(int VehicleId, string? VehicleName, decimal Liters, decimal Cost, DateTime FuelDate)>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();

        var daily = logs
            .GroupBy(l => l.FuelDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyFuelDto(g.Key, g.Sum(l => l.Liters), g.Sum(l => l.Cost)))
            .ToList();

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, request.DriverId, Unpaged: true),
            cancellationToken);

        var distanceByVehicle = tripsResponse is { Success: true, Data: not null }
            ? tripsResponse.Data.Items.GroupBy(t => t.VehicleId).ToDictionary(g => g.Key, g => (decimal)g.Sum(t => t.DistanceKm))
            : [];

        var byVehicle = logs
            .GroupBy(l => l.VehicleId)
            .Select(g =>
            {
                var distance = distanceByVehicle.GetValueOrDefault(g.Key);
                var liters = g.Sum(l => l.Liters);
                var litersPer100Km = distance > 0 ? Math.Round(liters / distance * 100, 2) : (decimal?)null;
                return new VehicleFuelDto(g.Key, g.First().VehicleName, liters, g.Sum(l => l.Cost), distance > 0 ? distance : null, litersPer100Km);
            })
            .OrderByDescending(v => v.Liters)
            .ToList();

        var totalLiters = logs.Sum(l => l.Liters);
        var totalDistance = distanceByVehicle.Values.Sum();
        var fleetLitersPer100Km = totalDistance > 0 ? Math.Round(totalLiters / totalDistance * 100, 2) : (decimal?)null;

        var dto = new FuelAnalyticsDto(totalLiters, logs.Sum(l => l.Cost), fleetLitersPer100Km, daily, byVehicle);
        return ApiResponse<FuelAnalyticsDto>.SuccessResponse(dto);
    }
}

public record GetCostAnalyticsQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null)
    : IRequest<ApiResponse<CostAnalyticsDto>>;

/// <summary>Cost/KM = (Fuel + Maintenance) / Distance only — confirmed scope decision, not a placeholder pending more data sources. CostBasisNote is rendered verbatim in the UI so this limitation is never silently hidden.</summary>
public class GetCostAnalyticsQueryHandler(IDbConnectionFactory dbFactory, IMediator mediator, ITenantContext tenantContext)
    : IRequestHandler<GetCostAnalyticsQuery, ApiResponse<CostAnalyticsDto>>
{
    private const string BasisNote = "Cost per KM includes fuel and maintenance costs only. Toll, parking, and driver cost are not tracked in this system.";

    public async Task<ApiResponse<CostAnalyticsDto>> Handle(GetCostAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var branchClause = request.BranchId.HasValue ? " AND v.BranchId = @BranchId" : "";
        var departmentClause = request.DepartmentId.HasValue ? " AND v.DepartmentId = @DepartmentId" : "";

        var fuelCost = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            $"""
            SELECT ISNULL(SUM(f.TotalCost), 0)
            FROM FuelLogs f
            INNER JOIN Vehicles v ON v.Id = f.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            WHERE f.IsDeleted = 0 AND f.FuelDate >= @FromDate AND f.FuelDate < @ToDate {branchClause} {departmentClause}
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate, request.BranchId, request.DepartmentId },
            cancellationToken: cancellationToken));

        var maintenanceCost = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            $"""
            SELECT ISNULL(SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)), 0)
            FROM Maintenance m
            INNER JOIN Vehicles v ON v.Id = m.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            WHERE m.IsDeleted = 0 AND m.MaintenanceDate >= @FromDate AND m.MaintenanceDate < @ToDate {branchClause} {departmentClause}
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate, request.BranchId, request.DepartmentId },
            cancellationToken: cancellationToken));

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, null, Unpaged: true),
            cancellationToken);

        var totalDistance = tripsResponse is { Success: true, Data: not null }
            ? (decimal)tripsResponse.Data.Items.Sum(t => t.DistanceKm)
            : 0;

        var totalCost = fuelCost + maintenanceCost;
        var costPerKm = totalDistance > 0 ? Math.Round(totalCost / totalDistance, 2) : (decimal?)null;

        var dto = new CostAnalyticsDto(fuelCost, maintenanceCost, totalCost, Math.Round(totalDistance, 2), costPerKm, BasisNote);
        return ApiResponse<CostAnalyticsDto>.SuccessResponse(dto);
    }
}
