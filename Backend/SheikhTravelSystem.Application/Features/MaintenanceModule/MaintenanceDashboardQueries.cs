using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record GetMaintenanceDashboardQuery(
    DateTime? From = null,
    DateTime? To = null,
    int? BranchId = null,
    string Period = "Month",
    string Granularity = "Day")
    : IRequest<ApiResponse<MaintenanceDashboardDto>>;

public class GetMaintenanceDashboardQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetMaintenanceDashboardQuery, ApiResponse<MaintenanceDashboardDto>>
{
    public async Task<ApiResponse<MaintenanceDashboardDto>> Handle(GetMaintenanceDashboardQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var (from, to) = ResolveRange(request.From, request.To, request.Period);
        using var connection = dbFactory.CreateConnection();

        var branchClause = request.BranchId.HasValue ? " AND v.BranchId = @BranchId" : "";
        var p = new { TenantId = tenantId, From = from, To = to, BranchId = request.BranchId };

        var kpis = await connection.QuerySingleAsync<MaintenanceKpiDto>(new CommandDefinition($"""
            SELECT
                (SELECT COUNT(*) FROM Vehicles v WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId AND v.Status <> @Retired {branchClause}) AS TotalVehicles,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1, 2)
                    LEFT JOIN VehicleMaintenanceSchedules s ON s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND (
                        (m.NextDueDate IS NOT NULL AND m.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()) AND m.NextDueDate >= GETUTCDATE())
                        OR (s.NextDueDate IS NOT NULL AND s.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()) AND s.NextDueDate >= GETUTCDATE())
                      )) AS DueForService,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN WorkOrders wo ON wo.VehicleId = v.Id AND wo.IsDeleted = 0 AND wo.Status IN (N'Assigned', N'InProgress', N'WaitingParts')
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND (v.Status = @Maintenance OR wo.Id IS NOT NULL)) AS UnderMaintenance,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1, 2)
                    LEFT JOIN VehicleMaintenanceSchedules s ON s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND (
                        (m.NextDueDate IS NOT NULL AND m.NextDueDate < GETUTCDATE())
                        OR (s.NextDueDate IS NOT NULL AND s.NextDueDate < GETUTCDATE())
                      )) AS OverdueServices,
                (SELECT ISNULL(SUM(ISNULL(m.Cost,0) + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)), 0)
                    FROM Maintenance m
                    INNER JOIN Vehicles v ON v.Id = m.VehicleId
                    WHERE m.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND m.MaintenanceDate >= @From AND m.MaintenanceDate < @To {branchClause})
                  + (SELECT ISNULL(SUM(wo.LaborCost + wo.PartsCost), 0)
                    FROM WorkOrders wo
                    INNER JOIN Vehicles v ON v.Id = wo.VehicleId
                    WHERE wo.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND wo.CreatedAt >= @From AND wo.CreatedAt < @To {branchClause}) AS MonthlyMaintenanceCost,
                (SELECT COUNT(*) FROM WorkOrders wo
                    INNER JOIN Vehicles v ON v.Id = wo.VehicleId
                    WHERE wo.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND wo.Status IN (N'Draft', N'Open', N'Assigned', N'InProgress', N'WaitingParts') {branchClause}) AS ActiveWorkOrders,
                (SELECT COUNT(*) FROM MaintenanceRequests r
                    INNER JOIN Vehicles v ON v.Id = r.VehicleId
                    WHERE r.IsDeleted = 0 AND r.TenantId = @TenantId
                      AND r.Status IN (N'Open', N'PendingApproval') {branchClause.Replace("v.", "v.")}) AS PendingRequests
            """, new
        {
            TenantId = tenantId,
            From = from,
            To = to,
            BranchId = request.BranchId,
            Retired = (int)VehicleStatus.Retired,
            Maintenance = (int)VehicleStatus.Maintenance
        }, cancellationToken: cancellationToken));

        var costTrend = await GetCostTrendAsync(connection, tenantId, from, to, request.Granularity, request.BranchId, cancellationToken);

        var health = await connection.QuerySingleAsync<VehicleHealthDto>(new CommandDefinition($"""
            SELECT
                (SELECT COUNT(*) FROM Vehicles v WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                    AND v.Status NOT IN (@Maintenance, @Retired)
                    AND NOT EXISTS (
                        SELECT 1 FROM Maintenance m WHERE m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1,2)
                          AND m.NextDueDate IS NOT NULL AND m.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()))
                    AND NOT EXISTS (
                        SELECT 1 FROM VehicleMaintenanceSchedules s WHERE s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                          AND s.NextDueDate IS NOT NULL AND s.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()))
                    AND NOT EXISTS (
                        SELECT 1 FROM WorkOrders wo WHERE wo.VehicleId = v.Id AND wo.IsDeleted = 0
                          AND wo.Status IN (N'Assigned', N'InProgress', N'WaitingParts'))) AS Healthy,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1,2)
                    LEFT JOIN VehicleMaintenanceSchedules s ON s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND v.Status NOT IN (@Maintenance, @Retired)
                      AND (
                        (m.NextDueDate IS NOT NULL AND m.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()) AND m.NextDueDate >= GETUTCDATE())
                        OR (s.NextDueDate IS NOT NULL AND s.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()) AND s.NextDueDate >= GETUTCDATE())
                      )) AS ServiceDueSoon,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1,2)
                    LEFT JOIN VehicleMaintenanceSchedules s ON s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND (
                        (m.NextDueDate IS NOT NULL AND m.NextDueDate < GETUTCDATE())
                        OR (s.NextDueDate IS NOT NULL AND s.NextDueDate < GETUTCDATE())
                      )) AS Overdue,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN WorkOrders wo ON wo.VehicleId = v.Id AND wo.IsDeleted = 0 AND wo.Status IN (N'Assigned', N'InProgress', N'WaitingParts')
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND (v.Status = @Maintenance OR wo.Id IS NOT NULL)) AS InWorkshop
            """, new
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            Maintenance = (int)VehicleStatus.Maintenance,
            Retired = (int)VehicleStatus.Retired
        }, cancellationToken: cancellationToken));

        var alerts = await connection.QueryAsync<MaintenanceAlertDto>(new CommandDefinition($"""
            SELECT TOP 10 a.Id, a.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehicleRegistration,
                   a.AlertType, a.Severity, a.Title, a.Message, a.CreatedAt
            FROM MaintenanceAlerts a
            LEFT JOIN Vehicles v ON v.Id = a.VehicleId
            WHERE a.TenantId = @TenantId AND a.IsDismissed = 0
              AND a.Severity IN (N'Critical', N'Error', N'Warning')
            ORDER BY a.CreatedAt DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        var recentWorkOrders = await connection.QueryAsync<WorkOrderListItemDto>(new CommandDefinition($"""
            SELECT TOP 5 {MaintenanceSql.WorkOrderListSelect}
            {MaintenanceSql.WorkOrderListFrom}
            WHERE wo.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause.Replace("v.", "v.")}
            ORDER BY wo.CreatedAt DESC
            """, new { TenantId = tenantId, BranchId = request.BranchId }, cancellationToken: cancellationToken));

        var upcoming = await connection.QueryAsync<UpcomingServiceDto>(new CommandDefinition($"""
            SELECT TOP 10 * FROM (
                SELECT s.Id AS ScheduleId, s.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehicleRegistration,
                       s.ServiceTypeName AS ServiceType, s.NextDueDate AS DueDate, s.NextDueMileage AS DueMileage, s.Priority
                FROM VehicleMaintenanceSchedules s
                INNER JOIN Vehicles v ON v.Id = s.VehicleId AND v.IsDeleted = 0
                WHERE s.IsDeleted = 0 AND s.IsActive = 1 AND s.TenantId = @TenantId
                  AND s.NextDueDate IS NOT NULL {branchClause}
                UNION ALL
                SELECT NULL, m.VehicleId, v.Name, v.RegistrationNumber,
                       ISNULL(m.MaintenanceType, m.Description), m.NextDueDate, m.NextDueMileage, ISNULL(m.Priority, N'Medium')
                FROM Maintenance m
                INNER JOIN Vehicles v ON v.Id = m.VehicleId AND v.IsDeleted = 0
                WHERE m.IsDeleted = 0 AND v.TenantId = @TenantId AND m.Status IN (1,2)
                  AND m.NextDueDate IS NOT NULL {branchClause}
            ) u
            ORDER BY DueDate ASC
            """, new { TenantId = tenantId, BranchId = request.BranchId }, cancellationToken: cancellationToken));

        var fuelSummary = await GetFuelSummaryAsync(connection, tenantId, from, to, request.BranchId, cancellationToken);

        return ApiResponse<MaintenanceDashboardDto>.SuccessResponse(new MaintenanceDashboardDto(
            kpis, costTrend, health, alerts.ToList(), recentWorkOrders.ToList(), upcoming.ToList(), fuelSummary));
    }

    private static (DateTime From, DateTime To) ResolveRange(DateTime? from, DateTime? to, string period)
    {
        if (from.HasValue && to.HasValue)
            return (from.Value.ToUniversalTime(), to.Value.ToUniversalTime());

        var now = DateTime.UtcNow;
        return period.ToLowerInvariant() switch
        {
            "today" => (now.Date, now.Date.AddDays(1)),
            "week" => (now.Date.AddDays(-(int)now.DayOfWeek), now.Date.AddDays(1)),
            "quarter" => (new DateTime(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1), now.Date.AddDays(1)),
            "year" => (new DateTime(now.Year, 1, 1), now.Date.AddDays(1)),
            _ => (new DateTime(now.Year, now.Month, 1), now.Date.AddDays(1))
        };
    }

    private static async Task<IReadOnlyList<MaintenanceCostTrendPointDto>> GetCostTrendAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        string granularity, int? branchId, CancellationToken ct)
    {
        var branchClause = branchId.HasValue ? " AND v.BranchId = @BranchId" : "";
        static string DateBucket(string column, string granularity) =>
            granularity.Equals("Month", StringComparison.OrdinalIgnoreCase)
                ? $"FORMAT({column}, 'yyyy-MM')"
                : granularity.Equals("Week", StringComparison.OrdinalIgnoreCase)
                    ? $"CONCAT(DATEPART(YEAR, {column}), '-W', DATEPART(WEEK, {column}))"
                    : $"FORMAT({column}, 'yyyy-MM-dd')";

        var maintBucket = DateBucket("m.MaintenanceDate", granularity);
        var woBucket = DateBucket("wo.CreatedAt", granularity);

        var rows = await connection.QueryAsync<(string Label, decimal Preventive, decimal Corrective, decimal Breakdown)>(
            new CommandDefinition($"""
                SELECT Label,
                    SUM(Preventive) AS Preventive,
                    SUM(Corrective) AS Corrective,
                    SUM(Breakdown) AS Breakdown
                FROM (
                    SELECT {maintBucket} AS Label,
                        CASE WHEN ISNULL(m.IsPreventive, 0) = 1
                            THEN ISNULL(m.Cost,0)+ISNULL(m.LaborCost,0)+ISNULL(m.PartsCost,0) ELSE 0 END AS Preventive,
                        CASE WHEN ISNULL(m.IsPreventive, 0) = 0 AND ISNULL(m.Category, N'') <> N'Breakdown'
                            THEN ISNULL(m.Cost,0)+ISNULL(m.LaborCost,0)+ISNULL(m.PartsCost,0) ELSE 0 END AS Corrective,
                        CASE WHEN m.Category = N'Breakdown'
                            THEN ISNULL(m.Cost,0)+ISNULL(m.LaborCost,0)+ISNULL(m.PartsCost,0) ELSE 0 END AS Breakdown
                    FROM Maintenance m
                    INNER JOIN Vehicles v ON v.Id = m.VehicleId
                    WHERE m.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND m.MaintenanceDate >= @From AND m.MaintenanceDate < @To {branchClause}

                    UNION ALL

                    SELECT {woBucket} AS Label,
                        CASE WHEN wo.MaintenanceType = N'Preventive'
                            THEN ISNULL(wo.LaborCost,0)+ISNULL(wo.PartsCost,0) ELSE 0 END,
                        CASE WHEN wo.MaintenanceType = N'Corrective'
                            THEN ISNULL(wo.LaborCost,0)+ISNULL(wo.PartsCost,0) ELSE 0 END,
                        CASE WHEN wo.MaintenanceType = N'Emergency'
                            THEN ISNULL(wo.LaborCost,0)+ISNULL(wo.PartsCost,0) ELSE 0 END
                    FROM WorkOrders wo
                    INNER JOIN Vehicles v ON v.Id = wo.VehicleId AND v.IsDeleted = 0
                    WHERE wo.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND wo.CreatedAt >= @From AND wo.CreatedAt < @To {branchClause}
                ) buckets
                GROUP BY Label
                ORDER BY Label
                """, new { TenantId = tenantId, From = from, To = to, BranchId = branchId }, cancellationToken: ct));

        return rows.Select(r => new MaintenanceCostTrendPointDto(r.Label, r.Preventive, r.Corrective, r.Breakdown)).ToList();
    }

    private static async Task<FuelMaintenanceSummaryDto?> GetFuelSummaryAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? branchId, CancellationToken ct)
    {
        var branchClause = branchId.HasValue ? " AND v.BranchId = @BranchId" : "";

        var monthly = await connection.QueryAsync<(string Label, decimal Fuel, decimal Maint)>(new CommandDefinition($"""
            SELECT FORMAT(d.MonthDate, 'MMM') AS Label,
                ISNULL(SUM(f.TotalCost), 0) AS Fuel,
                ISNULL(SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)), 0) AS Maint
            FROM (
                SELECT DATEFROMPARTS(YEAR(@From), MONTH(@From), 1) AS MonthDate
                UNION ALL SELECT DATEADD(MONTH, 1, DATEFROMPARTS(YEAR(@From), MONTH(@From), 1))
                UNION ALL SELECT DATEADD(MONTH, 2, DATEFROMPARTS(YEAR(@From), MONTH(@From), 1))
                UNION ALL SELECT DATEADD(MONTH, 3, DATEFROMPARTS(YEAR(@From), MONTH(@From), 1))
                UNION ALL SELECT DATEADD(MONTH, 4, DATEFROMPARTS(YEAR(@From), MONTH(@From), 1))
                UNION ALL SELECT DATEADD(MONTH, 5, DATEFROMPARTS(YEAR(@From), MONTH(@From), 1))
            ) d
            LEFT JOIN FuelLogs f ON DATEFROMPARTS(YEAR(f.FuelDate), MONTH(f.FuelDate), 1) = d.MonthDate AND f.IsDeleted = 0
            LEFT JOIN Vehicles vf ON vf.Id = f.VehicleId AND vf.TenantId = @TenantId {branchClause.Replace("v.", "vf.")}
            LEFT JOIN Maintenance m ON DATEFROMPARTS(YEAR(m.MaintenanceDate), MONTH(m.MaintenanceDate), 1) = d.MonthDate AND m.IsDeleted = 0
            LEFT JOIN Vehicles vm ON vm.Id = m.VehicleId AND vm.TenantId = @TenantId {branchClause.Replace("v.", "vm.")}
            GROUP BY d.MonthDate, FORMAT(d.MonthDate, 'MMM')
            ORDER BY d.MonthDate
            """, new { TenantId = tenantId, From = from, To = to, BranchId = branchId }, cancellationToken: ct));

        var list = monthly.ToList();
        if (list.Count == 0) return null;

        var highCost = await connection.QueryAsync<HighCostVehicleDto>(new CommandDefinition($"""
            SELECT TOP 5 VehicleId, VehicleName, FuelCost, MaintenanceCost
            FROM (
                SELECT v.Id AS VehicleId, v.Name AS VehicleName,
                    ISNULL((SELECT SUM(f.TotalCost) FROM FuelLogs f WHERE f.VehicleId = v.Id AND f.IsDeleted = 0
                        AND f.FuelDate >= @From AND f.FuelDate < @To), 0) AS FuelCost,
                    ISNULL((SELECT SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)) FROM Maintenance m
                        WHERE m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.MaintenanceDate >= @From AND m.MaintenanceDate < @To), 0) AS MaintenanceCost
                FROM Vehicles v
                WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
            ) ranked
            ORDER BY FuelCost + MaintenanceCost DESC
            """, new { TenantId = tenantId, From = from, To = to, BranchId = branchId }, cancellationToken: ct));

        return new FuelMaintenanceSummaryDto(
            list.Select(x => x.Label).ToList(),
            list.Select(x => x.Fuel).ToList(),
            list.Select(x => x.Maint).ToList(),
            highCost.ToList());
    }
}

public record GetMaintenanceAlertsQuery(int Limit = 20) : IRequest<ApiResponse<IReadOnlyList<MaintenanceAlertDto>>>;

public class GetMaintenanceAlertsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetMaintenanceAlertsQuery, ApiResponse<IReadOnlyList<MaintenanceAlertDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<MaintenanceAlertDto>>> Handle(GetMaintenanceAlertsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<MaintenanceAlertDto>(new CommandDefinition($"""
            SELECT TOP (@Limit) a.Id, a.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehicleRegistration,
                   a.AlertType, a.Severity, a.Title, a.Message, a.CreatedAt
            FROM MaintenanceAlerts a
            LEFT JOIN Vehicles v ON v.Id = a.VehicleId
            WHERE a.TenantId = @TenantId AND a.IsDismissed = 0
            ORDER BY a.CreatedAt DESC
            """, new { TenantId = tenantId, request.Limit }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<MaintenanceAlertDto>>.SuccessResponse(rows.ToList());
    }
}
