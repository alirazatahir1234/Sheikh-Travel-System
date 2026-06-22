using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record GetMaintenanceReportQuery(
    string ReportType = "cost-analysis",
    DateTime? From = null,
    DateTime? To = null,
    int? VehicleId = null,
    int? BranchId = null,
    string? Status = null)
    : IRequest<ApiResponse<MaintenanceReportDto>>;

public class GetMaintenanceReportQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetMaintenanceReportQuery, ApiResponse<MaintenanceReportDto>>
{
    public async Task<ApiResponse<MaintenanceReportDto>> Handle(
        GetMaintenanceReportQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var reportType = MaintenanceReportHelper.NormalizeReportType(request.ReportType);
        var (from, to) = MaintenanceReportHelper.ResolveDateRange(request.From, request.To);
        using var connection = dbFactory.CreateConnection();

        var report = reportType switch
        {
            "vehicle-maintenance" => await BuildVehicleMaintenanceAsync(connection, tenantId, from, to,
                request.VehicleId, request.BranchId, request.Status, cancellationToken),
            "service-due" => await BuildServiceDueAsync(connection, tenantId, request.VehicleId, request.BranchId,
                request.Status, cancellationToken),
            "overdue-maintenance" => await BuildOverdueAsync(connection, tenantId, request.VehicleId, request.BranchId,
                request.Status, cancellationToken),
            "workshop-performance" => await BuildWorkshopPerformanceAsync(connection, tenantId, from, to,
                request.Status, cancellationToken),
            "vendor-performance" => await BuildVendorPerformanceAsync(connection, tenantId, from, to, cancellationToken),
            "breakdown" => await BuildBreakdownAsync(connection, tenantId, from, to, request.VehicleId,
                request.BranchId, request.Status, cancellationToken),
            _ => await BuildCostAnalysisAsync(connection, tenantId, from, to, request.VehicleId, request.BranchId,
                cancellationToken)
        };

        return ApiResponse<MaintenanceReportDto>.SuccessResponse(report);
    }

    private static async Task<MaintenanceReportDto> BuildVehicleMaintenanceAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? vehicleId, int? branchId, string? status, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("plate", "Plate", "text"),
            new MaintenanceReportColumnDto("branch", "Branch", "text"),
            new MaintenanceReportColumnDto("serviceCount", "Services", "number"),
            new MaintenanceReportColumnDto("totalCost", "Total Cost", "currency"),
            new MaintenanceReportColumnDto("lastServiceDate", "Last Service", "date"),
            new MaintenanceReportColumnDto("status", "Status", "text")
        };

        var clauses = new List<string> { "v.IsDeleted = 0", "v.TenantId = @TenantId" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", from);
        p.Add("To", to);
        MaintenanceReportSql.ApplyVehicleBranchFilters(p, vehicleId, branchId, "v", null, clauses);
        var where = MaintenanceReportSql.BuildWhere(clauses);

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT v.Id, v.Name AS Label, v.RegistrationNumber AS Plate, ISNULL(b.Name, N'') AS BranchName,
                COUNT(m.Id) AS ServiceCount,
                ISNULL(SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)), 0) AS TotalCost,
                MAX(m.MaintenanceDate) AS LastServiceDate,
                CASE WHEN EXISTS (
                    SELECT 1 FROM WorkOrders wo
                    WHERE wo.VehicleId = v.Id AND wo.IsDeleted = 0
                      AND wo.Status IN (N'Open', N'Assigned', N'InProgress', N'WaitingParts')
                ) THEN N'Open' ELSE N'Completed' END AS RowStatus
            FROM Vehicles v
            LEFT JOIN Branches b ON b.Id = v.BranchId
            LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0
                AND m.MaintenanceDate >= @From AND m.MaintenanceDate < @To
            {where}
            GROUP BY v.Id, v.Name, v.RegistrationNumber, b.Name
            ORDER BY TotalCost DESC
            """, p, cancellationToken: ct));

        var rows = raw.Select(r =>
        {
            string rowStatus = r.RowStatus;
            return MaintenanceReportHelper.Row(
                ((int)r.Id).ToString(), (string)r.Label, (int)r.ServiceCount, (decimal)r.TotalCost,
                ("plate", (object?)r.Plate),
                ("branch", (object?)r.BranchName),
                ("serviceCount", (object?)(int)r.ServiceCount),
                ("totalCost", (object?)(decimal)r.TotalCost),
                ("lastServiceDate", (object?)r.LastServiceDate),
                ("status", (object?)rowStatus));
        }).Where(r => MaintenanceReportHelper.MatchesStatusFilter(status, r.Fields.GetValueOrDefault("status")?.ToString() ?? ""))
          .ToList();

        return new MaintenanceReportDto("vehicle-maintenance", MaintenanceReportHelper.TitleFor("vehicle-maintenance"),
            columns, rows, rows.Sum(r => r.TotalCost),
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }

    private static async Task<MaintenanceReportDto> BuildServiceDueAsync(
        System.Data.IDbConnection connection, int tenantId, int? vehicleId, int? branchId, string? status, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("vehicle", "Vehicle", "text"),
            new MaintenanceReportColumnDto("serviceType", "Service Type", "text"),
            new MaintenanceReportColumnDto("dueDate", "Due Date", "date"),
            new MaintenanceReportColumnDto("dueMileage", "Due Mileage", "number"),
            new MaintenanceReportColumnDto("daysUntilDue", "Days Until Due", "number"),
            new MaintenanceReportColumnDto("status", "Status", "text")
        };

        var clauses = new List<string> { "s.IsDeleted = 0", "s.IsActive = 1", "v.TenantId = @TenantId", "v.IsDeleted = 0" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        MaintenanceReportSql.ApplyVehicleBranchFilters(p, vehicleId, branchId, "v", null, clauses);
        var where = MaintenanceReportSql.BuildWhere(clauses);

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT s.Id, v.Name AS VehicleName, COALESCE(st.Name, s.ServiceTypeName) AS ServiceType,
                s.NextDueDate, s.NextDueMileage, v.CurrentMileage,
                DATEDIFF(day, GETUTCDATE(), s.NextDueDate) AS DaysUntilDue
            FROM VehicleMaintenanceSchedules s
            INNER JOIN Vehicles v ON v.Id = s.VehicleId
            LEFT JOIN ServiceTypes st ON st.Id = s.ServiceTypeId
            {where}
            """, p, cancellationToken: ct));

        var rows = new List<MaintenanceReportRowDto>();
        foreach (var r in raw)
        {
            DateTime? dueDate = r.NextDueDate;
            var scheduleStatus = dueDate.HasValue
                ? MaintenanceScheduleHelper.DateStatus(dueDate.Value.Date, DateTime.UtcNow.Date)
                : MaintenanceScheduleHelper.StatusUpcoming;

            if (status?.Equals("Scheduled", StringComparison.OrdinalIgnoreCase) == true &&
                scheduleStatus != MaintenanceScheduleHelper.StatusUpcoming)
                continue;
            if (!MaintenanceReportHelper.MatchesStatusFilter(status, scheduleStatus))
                continue;

            rows.Add(MaintenanceReportHelper.Row(
                ((int)r.Id).ToString(), (string)r.VehicleName, 1, 0m,
                ("vehicle", (object?)r.VehicleName),
                ("serviceType", (object?)r.ServiceType),
                ("dueDate", (object?)r.NextDueDate),
                ("dueMileage", (object?)r.NextDueMileage),
                ("daysUntilDue", (object?)r.DaysUntilDue),
                ("status", scheduleStatus)));
        }

        return new MaintenanceReportDto("service-due", MaintenanceReportHelper.TitleFor("service-due"),
            columns, rows, 0,
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }

    private static async Task<MaintenanceReportDto> BuildOverdueAsync(
        System.Data.IDbConnection connection, int tenantId, int? vehicleId, int? branchId, string? status, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("vehicle", "Vehicle", "text"),
            new MaintenanceReportColumnDto("branch", "Branch", "text"),
            new MaintenanceReportColumnDto("overdueItem", "Overdue Item", "text"),
            new MaintenanceReportColumnDto("dueDate", "Due Date", "date"),
            new MaintenanceReportColumnDto("daysOverdue", "Days Overdue", "number"),
            new MaintenanceReportColumnDto("status", "Status", "text")
        };

        var clauses = new List<string>
        {
            "v.IsDeleted = 0", "v.TenantId = @TenantId",
            "(s.NextDueDate < GETUTCDATE() OR m.NextDueDate < GETUTCDATE())"
        };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        MaintenanceReportSql.ApplyVehicleBranchFilters(p, vehicleId, branchId, "v", null, clauses);
        var where = MaintenanceReportSql.BuildWhere(clauses);

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT v.Id, v.Name AS VehicleName, ISNULL(b.Name, N'') AS BranchName,
                COALESCE(st.Name, N'Maintenance') AS OverdueItem,
                COALESCE(s.NextDueDate, m.NextDueDate) AS DueDate,
                DATEDIFF(day, COALESCE(s.NextDueDate, m.NextDueDate), GETUTCDATE()) AS DaysOverdue
            FROM Vehicles v
            LEFT JOIN Branches b ON b.Id = v.BranchId
            LEFT JOIN VehicleMaintenanceSchedules s ON s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
            LEFT JOIN ServiceTypes st ON st.Id = s.ServiceTypeId
            LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1,2)
            {where}
            """, p, cancellationToken: ct));

        var rows = raw.Select(r => MaintenanceReportHelper.Row(
            $"{r.Id}-{r.OverdueItem}", (string)r.VehicleName, 1, 0m,
            ("vehicle", (object?)r.VehicleName),
            ("branch", (object?)r.BranchName),
            ("overdueItem", (object?)r.OverdueItem),
            ("dueDate", (object?)r.DueDate),
            ("daysOverdue", (object?)r.DaysOverdue),
            ("status", MaintenanceScheduleHelper.StatusOverdue)))
            .Where(r => MaintenanceReportHelper.MatchesStatusFilter(status, MaintenanceScheduleHelper.StatusOverdue)
                || MaintenanceReportHelper.MatchesStatusFilter(status, "Overdue"))
            .ToList();

        return new MaintenanceReportDto("overdue-maintenance", MaintenanceReportHelper.TitleFor("overdue-maintenance"),
            columns, rows, 0,
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }

    private static async Task<MaintenanceReportDto> BuildWorkshopPerformanceAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to, string? status, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("workshop", "Workshop", "text"),
            new MaintenanceReportColumnDto("completedCount", "Completed", "number"),
            new MaintenanceReportColumnDto("openCount", "Open", "number"),
            new MaintenanceReportColumnDto("avgDays", "Avg Days", "number"),
            new MaintenanceReportColumnDto("totalCost", "Total Cost", "currency"),
            new MaintenanceReportColumnDto("rating", "Rating", "number")
        };

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition("""
            SELECT w.Id, w.Name AS WorkshopName, w.Rating,
                SUM(CASE WHEN wo.Status = N'Completed' THEN 1 ELSE 0 END) AS CompletedCount,
                SUM(CASE WHEN wo.Status IN (N'Open', N'Assigned', N'InProgress', N'WaitingParts') THEN 1 ELSE 0 END) AS OpenCount,
                AVG(CASE WHEN wo.CompletedAt IS NOT NULL THEN CAST(DATEDIFF(day, wo.CreatedAt, wo.CompletedAt) AS FLOAT) END) AS AvgDays,
                ISNULL(SUM(wo.LaborCost + wo.PartsCost), 0) AS TotalCost
            FROM Workshops w
            LEFT JOIN WorkOrders wo ON wo.WorkshopId = w.Id AND wo.IsDeleted = 0
                AND wo.CreatedAt >= @From AND wo.CreatedAt < @To
            WHERE w.TenantId = @TenantId AND w.IsDeleted = 0
            GROUP BY w.Id, w.Name, w.Rating
            ORDER BY TotalCost DESC
            """, new { TenantId = tenantId, From = from, To = to }, cancellationToken: ct));

        var rows = raw.Select(r =>
        {
            var open = (int)(r.OpenCount ?? 0);
            var completed = (int)(r.CompletedCount ?? 0);
            var rowStatus = open > 0 ? "Open" : "Completed";
            return (Row: MaintenanceReportHelper.Row(
                ((int)r.Id).ToString(), (string)r.WorkshopName, completed + open, (decimal)r.TotalCost,
                ("workshop", (object?)r.WorkshopName),
                ("completedCount", (object?)completed),
                ("openCount", (object?)open),
                ("avgDays", (object?)r.AvgDays),
                ("totalCost", (object?)(decimal)r.TotalCost),
                ("rating", (object?)r.Rating)),
                Status: rowStatus);
        }).Where(x => MaintenanceReportHelper.MatchesStatusFilter(status, x.Status))
          .Select(x => x.Row).ToList();

        return new MaintenanceReportDto("workshop-performance", MaintenanceReportHelper.TitleFor("workshop-performance"),
            columns, rows, rows.Sum(r => r.TotalCost),
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }

    private static async Task<MaintenanceReportDto> BuildVendorPerformanceAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("vendor", "Vendor", "text"),
            new MaintenanceReportColumnDto("category", "Category", "text"),
            new MaintenanceReportColumnDto("preferred", "Preferred", "text"),
            new MaintenanceReportColumnDto("partsSpend", "Parts Spend", "currency"),
            new MaintenanceReportColumnDto("usageCount", "Usage Count", "number"),
            new MaintenanceReportColumnDto("rating", "Rating", "number")
        };

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition("""
            SELECT v.Id, v.Name AS VendorName, v.Category, v.IsPreferred, v.Rating,
                (SELECT COUNT(*) FROM PartUsage pu
                    INNER JOIN Parts p ON p.Id = pu.PartId
                    WHERE p.Supplier = v.Name AND pu.TenantId = @TenantId
                      AND pu.UsedAt >= @From AND pu.UsedAt < @To) AS UsageCount,
                (SELECT ISNULL(SUM(pu.Quantity * pu.UnitCost), 0) FROM PartUsage pu
                    INNER JOIN Parts p ON p.Id = pu.PartId
                    WHERE p.Supplier = v.Name AND pu.TenantId = @TenantId
                      AND pu.UsedAt >= @From AND pu.UsedAt < @To) AS PartsSpend
            FROM Vendors v
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0
            ORDER BY PartsSpend DESC
            """, new { TenantId = tenantId, From = from, To = to }, cancellationToken: ct));

        var rows = raw.Select(r => MaintenanceReportHelper.Row(
            ((int)r.Id).ToString(), (string)r.VendorName, (int)r.UsageCount, (decimal)r.PartsSpend,
            ("vendor", (object?)r.VendorName),
            ("category", (object?)r.Category),
            ("preferred", (object?)((bool)r.IsPreferred ? "Yes" : "No")),
            ("partsSpend", (object?)(decimal)r.PartsSpend),
            ("usageCount", (object?)(int)r.UsageCount),
            ("rating", (object?)r.Rating))).ToList();

        return new MaintenanceReportDto("vendor-performance", MaintenanceReportHelper.TitleFor("vendor-performance"),
            columns, rows, rows.Sum(r => r.TotalCost),
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }

    private static async Task<MaintenanceReportDto> BuildCostAnalysisAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? vehicleId, int? branchId, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("category", "Category", "text"),
            new MaintenanceReportColumnDto("count", "Count", "number"),
            new MaintenanceReportColumnDto("maintenanceCost", "Maintenance Cost", "currency"),
            new MaintenanceReportColumnDto("workOrderCost", "Work Order Cost", "currency"),
            new MaintenanceReportColumnDto("totalCost", "Total Cost", "currency")
        };

        var clauses = new List<string> { "v.IsDeleted = 0", "v.TenantId = @TenantId", "m.IsDeleted = 0",
            "m.MaintenanceDate >= @From", "m.MaintenanceDate < @To" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", from);
        p.Add("To", to);
        MaintenanceReportSql.ApplyVehicleBranchFilters(p, vehicleId, branchId, "v", null, clauses);
        var where = MaintenanceReportSql.BuildWhere(clauses);

        var maint = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT ISNULL(m.Category, N'Other') AS Category, COUNT(*) AS Cnt,
                ISNULL(SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)), 0) AS MaintenanceCost
            FROM Maintenance m
            INNER JOIN Vehicles v ON v.Id = m.VehicleId
            {where}
            GROUP BY m.Category
            """, p, cancellationToken: ct));

        var woClauses = new List<string> { "wo.IsDeleted = 0", "wo.TenantId = @TenantId",
            "wo.CreatedAt >= @From", "wo.CreatedAt < @To", "wo.Status = N'Completed'" };
        if (vehicleId.HasValue) woClauses.Add("wo.VehicleId = @VehicleId");
        if (branchId.HasValue)
        {
            woClauses.Add("EXISTS (SELECT 1 FROM Vehicles v2 WHERE v2.Id = wo.VehicleId AND v2.BranchId = @BranchId)");
        }
        var woWhere = MaintenanceReportSql.BuildWhere(woClauses);

        var woTotal = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition($"""
            SELECT ISNULL(SUM(wo.LaborCost + wo.PartsCost), 0)
            FROM WorkOrders wo {woWhere}
            """, p, cancellationToken: ct));

        var rows = maint.Select(r => MaintenanceReportHelper.Row(
            (string)r.Category, (string)r.Category, (int)r.Cnt, (decimal)r.MaintenanceCost,
            ("category", (object?)r.Category),
            ("count", (object?)(int)r.Cnt),
            ("maintenanceCost", (object?)(decimal)r.MaintenanceCost),
            ("workOrderCost", (object?)0m),
            ("totalCost", (object?)(decimal)r.MaintenanceCost))).ToList();

        if (woTotal > 0)
        {
            rows.Add(MaintenanceReportHelper.Row("work-orders", "Work Orders", 1, woTotal,
                ("category", "Work Orders"),
                ("count", 1),
                ("maintenanceCost", 0m),
                ("workOrderCost", woTotal),
                ("totalCost", woTotal)));
        }

        var total = rows.Sum(r => r.TotalCost);
        return new MaintenanceReportDto("cost-analysis", MaintenanceReportHelper.TitleFor("cost-analysis"),
            columns, rows, total,
            new Dictionary<string, object?> { ["rowCount"] = rows.Count, ["workOrderCost"] = woTotal });
    }

    private static async Task<MaintenanceReportDto> BuildBreakdownAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? vehicleId, int? branchId, string? status, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("vehicle", "Vehicle", "text"),
            new MaintenanceReportColumnDto("breakdownDate", "Breakdown Date", "date"),
            new MaintenanceReportColumnDto("location", "Location", "text"),
            new MaintenanceReportColumnDto("repairCost", "Repair Cost", "currency"),
            new MaintenanceReportColumnDto("downtimeDays", "Downtime Days", "number"),
            new MaintenanceReportColumnDto("status", "Status", "text")
        };

        var clauses = new List<string> { "b.IsDeleted = 0", "v.TenantId = @TenantId", "v.IsDeleted = 0",
            "b.ReportedAt >= @From", "b.ReportedAt < @To" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", from);
        p.Add("To", to);
        MaintenanceReportSql.ApplyVehicleBranchFilters(p, vehicleId, branchId, "v", null, clauses);
        var where = MaintenanceReportSql.BuildWhere(clauses);

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT b.Id, v.Name AS VehicleName, b.BreakdownLocation, b.RepairCost, b.Status AS BreakdownStatus,
                b.ReportedAt,
                DATEDIFF(day, b.ReportedAt, COALESCE(b.ResolvedAt, GETUTCDATE())) AS DowntimeDays
            FROM VehicleBreakdowns b
            INNER JOIN Vehicles v ON v.Id = b.VehicleId
            {where}
            ORDER BY b.ReportedAt DESC
            """, p, cancellationToken: ct));

        var rows = raw.Select(r =>
        {
            var rowStatus = ((string)r.BreakdownStatus).Equals("Resolved", StringComparison.OrdinalIgnoreCase)
                ? "Resolved" : "Open";
            return (Row: MaintenanceReportHelper.Row(
                ((int)r.Id).ToString(), (string)r.VehicleName, 1, (decimal)r.RepairCost,
                ("vehicle", (object?)r.VehicleName),
                ("breakdownDate", (object?)r.ReportedAt),
                ("location", (object?)r.BreakdownLocation),
                ("repairCost", (object?)(decimal)r.RepairCost),
                ("downtimeDays", (object?)(int)r.DowntimeDays),
                ("status", rowStatus)),
                Status: rowStatus);
        }).Where(x => MaintenanceReportHelper.MatchesStatusFilter(status, x.Status))
          .Select(x => x.Row).ToList();

        return new MaintenanceReportDto("breakdown", MaintenanceReportHelper.TitleFor("breakdown"),
            columns, rows, rows.Sum(r => r.TotalCost),
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }
}
