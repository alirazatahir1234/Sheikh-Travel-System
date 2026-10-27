using System.Text;
using System.Text.Json;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Tools;

/// <summary>
/// Phase 2 allowlisted read-only reporting tools for the AI Assistant.
/// Parameterized SQL only; tenant always from <see cref="AiToolExecutionContext.TenantId"/>.
/// </summary>
public static class AssistantPeriodHelper
{
    public static (DateTime From, DateTime ToExclusive, string Label) Resolve(
        string? question,
        JsonElement? args)
    {
        if (args is { } el && el.ValueKind == JsonValueKind.Object)
        {
            DateTime? from = null;
            DateTime? to = null;
            if (el.TryGetProperty("from", out var f) && f.TryGetDateTime(out var fd)) from = fd.ToUniversalTime();
            if (el.TryGetProperty("to", out var t) && t.TryGetDateTime(out var td)) to = td.ToUniversalTime();
            if (from is not null && to is not null && to > from)
                return (from.Value, to.Value, "custom");
        }

        var q = (question ?? string.Empty).ToLowerInvariant();
        var utcNow = DateTime.UtcNow;
        var todayStart = DateTime.SpecifyKind(utcNow.Date, DateTimeKind.Utc);
        var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (q.Contains("previous month") || q.Contains("last month"))
        {
            var prev = monthStart.AddMonths(-1);
            return (prev, monthStart, "previous_month");
        }

        if (q.Contains("yesterday"))
            return (todayStart.AddDays(-1), todayStart, "yesterday");

        if (q.Contains("today") || q.Contains("daily"))
            return (todayStart, todayStart.AddDays(1), "today");

        if (q.Contains("last 7") || q.Contains("past week") || q.Contains("this week") || q.Contains("weekly"))
            return (todayStart.AddDays(-7), todayStart.AddDays(1), "last_7_days");

        if (q.Contains("last 30") || q.Contains("past 30"))
            return (todayStart.AddDays(-30), todayStart.AddDays(1), "last_30_days");

        if (q.Contains("this month") || q.Contains("monthly") || q.Contains("month to date") || q.Contains("mtd"))
            return (monthStart, monthStart.AddMonths(1), "this_month");

        // Default: calendar month to date (UTC)
        return (monthStart, monthStart.AddMonths(1), "this_month");
    }

    public static JsonElement PeriodArgs(DateTime from, DateTime toExclusive)
        => JsonSerializer.SerializeToElement(new { from, to = toExclusive });

    /// <summary>Standard daily / weekly / monthly UTC windows for multi-bucket analytics.</summary>
    public static (DateTime TodayFrom, DateTime TodayTo, DateTime WeekFrom, DateTime WeekTo, DateTime MonthFrom, DateTime MonthTo)
        StandardBucketsUtc()
    {
        var utcNow = DateTime.UtcNow;
        var todayStart = DateTime.SpecifyKind(utcNow.Date, DateTimeKind.Utc);
        var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (
            todayStart, todayStart.AddDays(1),
            todayStart.AddDays(-7), todayStart.AddDays(1),
            monthStart, monthStart.AddMonths(1));
    }
}

public sealed class GetFuelCostSummaryTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetFuelCostSummary";
    public string Description => "Summarizes fuel cost and liters by vehicle for a reporting period.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
        ["fuel cost", "fuel expenditure", "fuel spend", "highest fuel", "fuel summary", "fuel expense"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        // Fleet-wide totals via SQL — never sum truncated TOP N rows.
        var totals = await connection.QuerySingleAsync<(decimal TotalCost, decimal TotalLiters, int EntryCount, int VehicleCount)>(
            new CommandDefinition("""
            SELECT
                ISNULL(SUM(f.TotalCost), 0) AS TotalCost,
                ISNULL(SUM(f.Liters), 0) AS TotalLiters,
                COUNT(*) AS EntryCount,
                COUNT(DISTINCT f.VehicleId) AS VehicleCount
            FROM FuelLogs f
            INNER JOIN Vehicles v ON v.Id = f.VehicleId AND v.TenantId = f.TenantId AND v.IsDeleted = 0
            WHERE f.IsDeleted = 0
              AND f.TenantId = @TenantId
              AND f.FuelDate >= @From
              AND f.FuelDate < @To
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken));

        var rows = (await connection.QueryAsync<FuelCostRow>(new CommandDefinition("""
            SELECT TOP 25
                v.Id AS VehicleId,
                v.RegistrationNumber AS PlateNumber,
                ISNULL(v.Name, v.RegistrationNumber) AS VehicleName,
                COUNT(*) AS EntryCount,
                ISNULL(SUM(f.Liters), 0) AS TotalLiters,
                ISNULL(SUM(f.TotalCost), 0) AS TotalCost
            FROM FuelLogs f
            INNER JOIN Vehicles v ON v.Id = f.VehicleId AND v.TenantId = f.TenantId AND v.IsDeleted = 0
            WHERE f.IsDeleted = 0
              AND f.TenantId = @TenantId
              AND f.FuelDate >= @From
              AND f.FuelDate < @To
            GROUP BY v.Id, v.RegistrationNumber, v.Name
            ORDER BY TotalCost DESC
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        var grandTotal = totals.TotalCost;
        var totalLiters = totals.TotalLiters;

        if (totals.EntryCount == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"No fuel entries found for {from:yyyy-MM-dd} … {to:yyyy-MM-dd} (UTC).",
                new
                {
                    period = new { start = from, end = to },
                    vehicles = rows,
                    grandTotal,
                    totalLiters,
                    entryCount = 0,
                    vehicleCount = 0
                });
        }

        var sb = new StringBuilder();
        sb.AppendLine(
            $"Fuel cost summary {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC): total {grandTotal:N2} across {totals.VehicleCount} vehicle(s), {totalLiters:N1} L " +
            $"(showing top {rows.Count}; fleet totals from SQL SUM).");
        foreach (var r in rows.Take(10))
            sb.AppendLine($"• {r.VehicleName} ({r.PlateNumber}): {r.TotalCost:N2} ({r.TotalLiters:N1} L, {r.EntryCount} entries)");

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new
            {
                period = new { start = from, end = to },
                vehicles = rows,
                grandTotal,
                totalLiters,
                entryCount = totals.EntryCount,
                vehicleCount = totals.VehicleCount
            });
    }

    private sealed record FuelCostRow(
        int VehicleId,
        string PlateNumber,
        string VehicleName,
        int EntryCount,
        decimal TotalLiters,
        decimal TotalCost);
}

public sealed class GetVehicleMaintenanceSummaryTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetVehicleMaintenanceSummary";
    public string Description =>
        "Surfaces Smart Operations maintenance-due alerts, schedule dues, and Maintenance work-order dues (read-only).";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "maintenance due", "service due", "maintenance overdue", "which vehicles have maintenance",
        "which vehicles need maintenance", "need maintenance", "vehicles need maintenance",
        "maintenance summary", "maintenance alert", "smart alert maintenance"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // Batch 2: reuse SmartAlerts produced by Smart Ops scanner — do not re-scan rules here.
        var smartAlerts = (await connection.QueryAsync<SmartMaintAlertRow>(new CommandDefinition("""
            SELECT TOP 25 Id, Severity, Message, Status, RelatedEntityId, CreatedAt
            FROM SmartAlerts
            WHERE TenantId = @TenantId
              AND RuleType = N'MaintenanceDue'
              AND Status IN (N'New', N'Acknowledged')
            ORDER BY
                CASE Severity WHEN N'Critical' THEN 0 WHEN N'Warning' THEN 1 ELSE 2 END,
                CreatedAt DESC
            """, new { context.TenantId }, cancellationToken: cancellationToken))).ToList();

        // Align with MaintenanceSql / schema: RegistrationNumber + NextDueMileage (alias NextServiceMileage).
        var rows = (await connection.QueryAsync<MaintRow>(new CommandDefinition("""
            SELECT TOP 25
                v.Id AS VehicleId,
                v.RegistrationNumber AS PlateNumber,
                ISNULL(v.Name, v.RegistrationNumber) AS VehicleName,
                ISNULL(NULLIF(s.ServiceTypeName, N''), ISNULL(st.Name, N'Service')) AS ServiceName,
                s.NextDueDate,
                s.NextDueMileage AS NextServiceMileage,
                CASE
                    WHEN s.NextDueDate IS NOT NULL AND s.NextDueDate < GETUTCDATE() THEN N'Overdue'
                    WHEN s.NextDueDate IS NOT NULL AND s.NextDueDate < DATEADD(DAY, 14, GETUTCDATE()) THEN N'DueSoon'
                    ELSE N'Scheduled'
                END AS Status,
                s.GeofenceId AS ZoneId,
                COALESCE(s.ZoneName, g.Name) AS ZoneName,
                COALESCE(s.ZoneCategory, g.Category) AS ZoneCategory,
                COALESCE(s.ZoneAreaType, g.AreaType) AS ZoneAreaType
            FROM VehicleMaintenanceSchedules s
            INNER JOIN Vehicles v ON v.Id = s.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            LEFT JOIN ServiceTypes st ON st.Id = s.ServiceTypeId
            LEFT JOIN Geofences g ON g.Id = s.GeofenceId AND g.IsDeleted = 0
            WHERE s.IsDeleted = 0 AND s.IsActive = 1
              AND (s.TenantId = @TenantId OR s.TenantId IS NULL)
              AND (
                    s.NextDueDate IS NOT NULL AND s.NextDueDate < DATEADD(DAY, 30, GETUTCDATE())
                 OR s.NextDueMileage IS NOT NULL
              )
            ORDER BY
                CASE WHEN s.NextDueDate < GETUTCDATE() THEN 0 ELSE 1 END,
                s.NextDueDate ASC
            """, new { context.TenantId }, cancellationToken: cancellationToken))).ToList();

        // Same due logic as SheikhGo MCP get_maintenance_due (Maintenance work-order table).
        var workOrders = (await connection.QueryAsync<WorkOrderDueRow>(new CommandDefinition("""
            SELECT TOP 25
                m.Id AS MaintenanceId,
                v.Id AS VehicleId,
                v.RegistrationNumber AS PlateNumber,
                ISNULL(v.Name, v.RegistrationNumber) AS VehicleName,
                m.Description AS ServiceName,
                m.NextDueDate,
                m.NextDueMileage AS NextServiceMileage,
                CASE
                    WHEN m.NextDueDate IS NOT NULL AND m.NextDueDate < GETUTCDATE() THEN N'Overdue'
                    WHEN m.Status IN (1, 2) THEN N'Due'
                    WHEN v.Status = 3 THEN N'VehicleInMaintenance'
                    WHEN m.NextDueDate IS NOT NULL THEN N'Upcoming'
                    ELSE N'Watch'
                END AS Status,
                m.GeofenceId AS ZoneId,
                COALESCE(m.ZoneName, g.Name) AS ZoneName,
                COALESCE(m.ZoneCategory, g.Category) AS ZoneCategory,
                COALESCE(m.ZoneAreaType, g.AreaType) AS ZoneAreaType
            FROM Maintenance m
            INNER JOIN Vehicles v ON v.Id = m.VehicleId AND v.IsDeleted = 0 AND v.TenantId = @TenantId
            LEFT JOIN Geofences g ON g.Id = m.GeofenceId AND g.IsDeleted = 0
            WHERE m.IsDeleted = 0
              AND (m.TenantId = @TenantId OR m.TenantId IS NULL)
              AND (
                    m.Status IN (1, 2)
                 OR (m.NextDueDate IS NOT NULL AND m.NextDueDate <= DATEADD(DAY, 30, GETUTCDATE()))
                 OR v.Status = 3
              )
            ORDER BY
                CASE
                    WHEN m.NextDueDate IS NOT NULL AND m.NextDueDate < GETUTCDATE() THEN 0
                    WHEN m.Status IN (1, 2) THEN 1
                    ELSE 2
                END,
                m.NextDueDate ASC
            """, new { context.TenantId }, cancellationToken: cancellationToken))).ToList();

        if (smartAlerts.Count == 0 && rows.Count == 0 && workOrders.Count == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                "No vehicles need maintenance right now — no Smart Ops alerts, schedules, or work orders due within 30 days.",
                new { smartAlerts, schedules = rows, workOrders });
        }

        var overdue = rows.Count(r => r.Status == "Overdue") + workOrders.Count(r => r.Status == "Overdue");
        var sb = new StringBuilder();
        sb.AppendLine(
            $"Vehicles needing maintenance: {smartAlerts.Count} Smart Ops alert(s), " +
            $"{rows.Count} schedule(s), {workOrders.Count} work order(s) ({overdue} overdue).");
        if (smartAlerts.Count > 0)
        {
            sb.AppendLine("Smart alerts:");
            foreach (var a in smartAlerts.Take(10))
                sb.AppendLine($"• [{a.Severity}/{a.Status}] {a.Message} (entity #{a.RelatedEntityId?.ToString() ?? "n/a"})");
        }

        if (rows.Count > 0)
        {
            sb.AppendLine("Schedules:");
            foreach (var r in rows.Take(10))
            {
                var zone = string.IsNullOrWhiteSpace(r.ZoneName) ? "" : $", zone {r.ZoneName}";
                sb.AppendLine(
                    $"• {r.VehicleName} ({r.PlateNumber}) — {r.ServiceName}: {r.Status}, " +
                    $"due {(r.NextDueDate?.ToString("yyyy-MM-dd") ?? "n/a")}{zone}");
            }
        }

        if (workOrders.Count > 0)
        {
            sb.AppendLine("Work orders / service records:");
            foreach (var r in workOrders.Take(10))
            {
                var zone = string.IsNullOrWhiteSpace(r.ZoneName) ? "" : $", zone {r.ZoneName}";
                sb.AppendLine(
                    $"• {r.VehicleName} ({r.PlateNumber}) — {r.ServiceName}: {r.Status}, " +
                    $"due {(r.NextDueDate?.ToString("yyyy-MM-dd") ?? "n/a")}{zone}");
            }
        }

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new { smartAlerts, schedules = rows, workOrders });
    }

    private sealed record SmartMaintAlertRow(
        long Id,
        string Severity,
        string Message,
        string Status,
        int? RelatedEntityId,
        DateTime CreatedAt);

    private sealed record MaintRow(
        int VehicleId,
        string PlateNumber,
        string VehicleName,
        string ServiceName,
        DateTime? NextDueDate,
        decimal? NextServiceMileage,
        string Status,
        int? ZoneId,
        string? ZoneName,
        string? ZoneCategory,
        string? ZoneAreaType);

    private sealed record WorkOrderDueRow(
        int MaintenanceId,
        int VehicleId,
        string PlateNumber,
        string VehicleName,
        string ServiceName,
        DateTime? NextDueDate,
        decimal? NextServiceMileage,
        string Status,
        int? ZoneId,
        string? ZoneName,
        string? ZoneCategory,
        string? ZoneAreaType);
}

public sealed class GetOverdueInvoicesTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetOverdueInvoices";
    public string Description => "Lists customer invoices with outstanding balance past due (or aged issued invoices).";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
        ["overdue invoice", "unpaid invoice", "invoices overdue", "which invoices", "outstanding invoice"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // DueDate may be added by Smart Ops migration; fall back to IssuedAt via dynamic SQL-safe COALESCE only if column exists.
        var hasDueDate = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN COL_LENGTH('dbo.CustomerInvoices', 'DueDate') IS NULL THEN 0 ELSE 1 END
            """, cancellationToken: cancellationToken));

        var dueExpr = hasDueDate == 1 ? "ISNULL(i.DueDate, i.IssuedAt)" : "i.IssuedAt";

        var rows = (await connection.QueryAsync<InvoiceRow>(new CommandDefinition($"""
            SELECT TOP 25
                i.Id,
                i.InvoiceNumber,
                i.CustomerName,
                i.TotalAmount,
                i.PaidAmount,
                i.Balance,
                i.Status,
                i.IssuedAt,
                {dueExpr} AS EffectiveDueDate
            FROM CustomerInvoices i
            WHERE i.IsDeleted = 0
              AND i.TenantId = @TenantId
              AND i.Balance > 0
              AND i.Status NOT IN (N'Paid', N'Void', N'Draft')
              AND {dueExpr} < GETUTCDATE()
            ORDER BY {dueExpr} ASC
            """, new { context.TenantId }, cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
            return new AiToolExecutionResult(Name, true, "No overdue invoices with outstanding balance.", rows);

        var totalBalance = rows.Sum(r => r.Balance);
        var sb = new StringBuilder();
        sb.AppendLine($"Overdue invoices: {rows.Count} totaling {totalBalance:N2} outstanding.");
        foreach (var r in rows.Take(15))
            sb.AppendLine($"• {r.InvoiceNumber} — {r.CustomerName ?? "customer"}: balance {r.Balance:N2}, due {r.EffectiveDueDate:yyyy-MM-dd}, status {r.Status}");

        return new AiToolExecutionResult(Name, true, sb.ToString().Trim(), new { invoices = rows, totalBalance });
    }

    private sealed record InvoiceRow(
        int Id,
        string InvoiceNumber,
        string? CustomerName,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal Balance,
        string Status,
        DateTime? IssuedAt,
        DateTime EffectiveDueDate);
}

public sealed class GetTripSummaryTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetTripSummary";
    public string Description =>
        "Trip and booking analytics: trip counts by status plus booking volume, completion, and cancellation rates.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "trip summary", "trips completed", "how many trips", "trip count", "completed trips",
        "booking analytics", "booking summary", "how many bookings", "completion rate", "trip and booking"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        var rows = (await connection.QueryAsync<TripStatusCount>(new CommandDefinition("""
            SELECT t.Status, COUNT(*) AS Cnt
            FROM Trips t
            WHERE t.IsDeleted = 0
              AND t.TenantId = @TenantId
              AND t.TripDate >= @From
              AND t.TripDate < @To
            GROUP BY t.Status
            ORDER BY Cnt DESC
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        var total = rows.Sum(r => r.Cnt);
        var completed = rows.Where(r => r.Status == (int)TripStatus.Completed).Sum(r => r.Cnt);
        var cancelled = rows.Where(r => r.Status == (int)TripStatus.Cancelled).Sum(r => r.Cnt);

        var bookings = await connection.QuerySingleAsync<BookingKpiRow>(new CommandDefinition("""
            SELECT
                COUNT(*) AS TotalBookings,
                ISNULL(SUM(CASE WHEN Status = @Completed THEN 1 ELSE 0 END), 0) AS Completed,
                ISNULL(SUM(CASE WHEN Status = @Cancelled THEN 1 ELSE 0 END), 0) AS Cancelled,
                ISNULL(SUM(CASE WHEN Status = @Pending THEN 1 ELSE 0 END), 0) AS Pending,
                ISNULL(SUM(CASE WHEN Status IN (@Confirmed, @Started) THEN 1 ELSE 0 END), 0) AS Active
            FROM Bookings
            WHERE CreatedAt >= @From AND CreatedAt < @To AND IsDeleted = 0 AND TenantId = @TenantId
            """, new
        {
            context.TenantId,
            From = from,
            To = to,
            Completed = (int)BookingStatus.Completed,
            Cancelled = (int)BookingStatus.Cancelled,
            Pending = (int)BookingStatus.Pending,
            Confirmed = (int)BookingStatus.Confirmed,
            Started = (int)BookingStatus.Started
        }, cancellationToken: cancellationToken));

        var bookingCompletionPct = bookings.TotalBookings > 0
            ? Math.Round(bookings.Completed * 100m / bookings.TotalBookings, 1)
            : 0m;

        if (total == 0 && bookings.TotalBookings == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"No trips or bookings found for {from:yyyy-MM-dd} … {to:yyyy-MM-dd} (UTC).",
                new { period = new { start = from, end = to }, trips = new { byStatus = rows, total, completed, cancelled }, bookings });
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Trip & booking analytics {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC):");
        sb.AppendLine($"• Trips: {total} total, {completed} completed, {cancelled} cancelled.");
        sb.AppendLine(
            $"• Bookings: {bookings.TotalBookings} total, {bookings.Completed} completed ({bookingCompletionPct}%), " +
            $"{bookings.Cancelled} cancelled, {bookings.Pending} pending, {bookings.Active} active.");

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new
            {
                period = new { start = from, end = to },
                trips = new { byStatus = rows, total, completed, cancelled },
                bookings = new
                {
                    bookings.TotalBookings,
                    bookings.Completed,
                    bookings.Cancelled,
                    bookings.Pending,
                    bookings.Active,
                    completionRatePercent = bookingCompletionPct
                }
            });
    }

    private sealed record TripStatusCount(int Status, int Cnt);

    private sealed record BookingKpiRow(
        int TotalBookings,
        int Completed,
        int Cancelled,
        int Pending,
        int Active);
}

public sealed class GetVehicleUtilizationSummaryTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetVehicleUtilizationSummary";
    public string Description =>
        "Fleet utilization rate from status snapshots plus vehicle ranking by GPS trip distance/duration.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "utilization", "utilization rate", "highest utilization", "most used vehicle",
        "vehicle usage", "distance traveled", "fleet utilization"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        // Fleet utilization rate from GpsFleetStatusSnapshots (same basis as analytics utilization API).
        var snapshots = (await connection.QueryAsync<SnapshotRow>(new CommandDefinition("""
            SELECT SnapshotAt, TotalVehicles, Moving, Idle, Parked, Offline
            FROM GpsFleetStatusSnapshots
            WHERE TenantId = @TenantId AND SnapshotAt >= @From AND SnapshotAt < @To
            ORDER BY SnapshotAt ASC
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        decimal? utilizationPercent = null;
        string rateNote;
        if (snapshots.Count == 0)
        {
            rateNote =
                "Fleet utilization rate unavailable — no GpsFleetStatusSnapshots in this period " +
                "(snapshots are required for % rate; vehicle GPS trip ranking may still apply).";
        }
        else
        {
            var totalAvailable = snapshots.Sum(s => s.TotalVehicles);
            var moving = snapshots.Sum(s => s.Moving);
            utilizationPercent = totalAvailable > 0
                ? Math.Round(moving * 100m / totalAvailable, 1)
                : 0m;
            rateNote =
                $"Fleet utilization rate: {utilizationPercent}% " +
                $"(moving vehicle-slots / available slots across {snapshots.Count} snapshot(s)).";
        }

        // GpsTrips may or may not have TenantId depending on schema age — always join Vehicles for tenant filter.
        var rows = (await connection.QueryAsync<UtilRow>(new CommandDefinition("""
            SELECT TOP 25
                v.Id AS VehicleId,
                v.RegistrationNumber AS PlateNumber,
                ISNULL(v.Name, v.RegistrationNumber) AS VehicleName,
                COUNT(*) AS TripCount,
                ISNULL(SUM(t.DistanceKm), 0) AS TotalDistanceKm,
                ISNULL(SUM(t.DurationMinutes), 0) AS TotalDurationMinutes
            FROM GpsTrips t
            INNER JOIN Vehicles v ON v.Id = t.VehicleId AND v.IsDeleted = 0 AND v.TenantId = @TenantId
            WHERE t.StartTime >= @From
              AND t.StartTime < @To
            GROUP BY v.Id, v.RegistrationNumber, v.Name
            ORDER BY TotalDistanceKm DESC, TotalDurationMinutes DESC
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        if (snapshots.Count == 0 && rows.Count == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"No utilization data for {from:yyyy-MM-dd} … {to:yyyy-MM-dd} (UTC). {rateNote}",
                new { period = new { start = from, end = to }, utilizationPercent, vehicles = rows });
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Vehicle utilization {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC):");
        sb.AppendLine($"• {rateNote}");
        if (rows.Count == 0)
            sb.AppendLine("• No GPS trip distance ranking available for this period.");
        else
            foreach (var r in rows.Take(10))
                sb.AppendLine($"• {r.VehicleName} ({r.PlateNumber}): {r.TotalDistanceKm:N1} km, {r.TotalDurationMinutes:N0} min, {r.TripCount} GPS trips");

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new
            {
                period = new { start = from, end = to },
                utilizationPercent,
                snapshotCount = snapshots.Count,
                vehicles = rows
            });
    }

    private sealed record SnapshotRow(
        DateTime SnapshotAt,
        int TotalVehicles,
        int Moving,
        int Idle,
        int Parked,
        int Offline);

    private sealed record UtilRow(
        int VehicleId,
        string PlateNumber,
        string VehicleName,
        int TripCount,
        decimal TotalDistanceKm,
        decimal TotalDurationMinutes);
}

/// <summary>Batch 2 — fuel + maintenance cost divided by GPS trip distance (tenant-scoped).</summary>
public sealed class GetCostPerKilometerTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetCostPerKilometer";
    public string Description =>
        "Computes fleet cost per kilometer from fuel + maintenance expenses over GPS trip distance.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "cost per kilometer", "cost per km", "cost/km", "per km cost", "expense per km", "operating cost per km"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        var fuelCost = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT ISNULL(SUM(TotalCost), 0) FROM FuelLogs
            WHERE TenantId = @TenantId AND FuelDate >= @From AND FuelDate < @To AND IsDeleted = 0
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken));

        var maintenanceCost = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT ISNULL(SUM(Cost), 0) FROM Maintenance
            WHERE TenantId = @TenantId AND MaintenanceDate >= @From AND MaintenanceDate < @To AND IsDeleted = 0
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken));

        var distanceKm = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT ISNULL(SUM(t.DistanceKm), 0)
            FROM GpsTrips t
            INNER JOIN Vehicles v ON v.Id = t.VehicleId AND v.IsDeleted = 0 AND v.TenantId = @TenantId
            WHERE t.StartTime >= @From AND t.StartTime < @To
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken));

        var totalCost = fuelCost + maintenanceCost;

        if (distanceKm <= 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"Cost per km not available for {from:yyyy-MM-dd} … {to:yyyy-MM-dd} (UTC): " +
                $"no GPS trip distance recorded (fuel cost {fuelCost:N2}, maintenance {maintenanceCost:N2}). " +
                "Cannot invent a rate without distance.",
                new
                {
                    period = new { start = from, end = to },
                    fuelCost,
                    maintenanceCost,
                    totalCost,
                    distanceKm,
                    costPerKm = (decimal?)null
                });
        }

        var costPerKm = Math.Round(totalCost / distanceKm, 4);
        var summary =
            $"Cost per km {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC): {costPerKm:N4} " +
            $"(fuel {fuelCost:N2} + maintenance {maintenanceCost:N2} = {totalCost:N2} over {distanceKm:N1} km). " +
            "FleetExpenses / TripExpenses are not included in this rate.";

        return new AiToolExecutionResult(
            Name, true, summary,
            new
            {
                period = new { start = from, end = to },
                fuelCost,
                maintenanceCost,
                totalCost,
                distanceKm,
                costPerKm
            });
    }
}

/// <summary>Batch 2 — overspeed and geofence breach counts from GpsAlertEvents.</summary>
public sealed class GetOverspeedGeofenceSummaryTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetOverspeedGeofenceSummary";
    public string Description =>
        "Summarizes overspeed and geofence enter/exit alert events for the tenant in a period.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "overspeed", "geofence", "geofence violation", "speed exceeded", "violation summary",
        "overspeed and geofence", "geofence breach"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        var rows = (await connection.QueryAsync<ViolationRow>(new CommandDefinition("""
            SELECT
                e.EventType,
                COUNT(*) AS Cnt,
                COUNT(DISTINCT e.VehicleId) AS VehicleCount
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.IsDeleted = 0 AND v.TenantId = @TenantId
            WHERE e.IsDeleted = 0
              AND e.Timestamp >= @From AND e.Timestamp < @To
              AND e.EventType IN (N'speed_exceeded', N'overspeed', N'geofence_enter', N'geofence_exit')
            GROUP BY e.EventType
            ORDER BY Cnt DESC
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        var overspeed = rows
            .Where(r => r.EventType is "speed_exceeded" or "overspeed")
            .Sum(r => r.Cnt);
        var geofence = rows
            .Where(r => r.EventType is "geofence_enter" or "geofence_exit")
            .Sum(r => r.Cnt);

        var topVehicles = (await connection.QueryAsync<TopVehicleRow>(new CommandDefinition("""
            SELECT TOP 10
                v.Id AS VehicleId,
                v.RegistrationNumber AS PlateNumber,
                ISNULL(v.Name, v.RegistrationNumber) AS VehicleName,
                SUM(CASE WHEN e.EventType IN (N'speed_exceeded', N'overspeed') THEN 1 ELSE 0 END) AS OverspeedCount,
                SUM(CASE WHEN e.EventType IN (N'geofence_enter', N'geofence_exit') THEN 1 ELSE 0 END) AS GeofenceCount
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.IsDeleted = 0 AND v.TenantId = @TenantId
            WHERE e.IsDeleted = 0
              AND e.Timestamp >= @From AND e.Timestamp < @To
              AND e.EventType IN (N'speed_exceeded', N'overspeed', N'geofence_enter', N'geofence_exit')
            GROUP BY v.Id, v.RegistrationNumber, v.Name
            ORDER BY (SUM(CASE WHEN e.EventType IN (N'speed_exceeded', N'overspeed') THEN 1 ELSE 0 END)
                    + SUM(CASE WHEN e.EventType IN (N'geofence_enter', N'geofence_exit') THEN 1 ELSE 0 END)) DESC
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        if (overspeed == 0 && geofence == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"No overspeed or geofence events for {from:yyyy-MM-dd} … {to:yyyy-MM-dd} (UTC).",
                new { period = new { start = from, end = to }, byType = rows, overspeed, geofence, topVehicles });
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Overspeed & geofence summary {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC):");
        sb.AppendLine($"• Overspeed events: {overspeed}");
        sb.AppendLine($"• Geofence enter/exit events: {geofence}");
        foreach (var r in rows)
            sb.AppendLine($"• {r.EventType}: {r.Cnt} ({r.VehicleCount} vehicle(s))");
        if (topVehicles.Count > 0)
        {
            sb.AppendLine("Top vehicles:");
            foreach (var v in topVehicles.Take(5))
                sb.AppendLine($"• {v.VehicleName} ({v.PlateNumber}): overspeed {v.OverspeedCount}, geofence {v.GeofenceCount}");
        }

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new { period = new { start = from, end = to }, byType = rows, overspeed, geofence, topVehicles });
    }

    private sealed record ViolationRow(string EventType, int Cnt, int VehicleCount);

    private sealed record TopVehicleRow(
        int VehicleId,
        string PlateNumber,
        string VehicleName,
        int OverspeedCount,
        int GeofenceCount);
}

/// <summary>Batch 1 — paid payment totals for today, last 7 days, and calendar month (UTC).</summary>
public sealed class GetRevenueAnalyticsTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetRevenueAnalytics";
    public string Description => "Returns daily, weekly, and monthly paid revenue totals for the tenant.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "revenue", "daily revenue", "weekly revenue", "monthly revenue",
        "revenue analytics", "revenue total", "how much revenue", "sales total", "paid payments"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (todayFrom, todayTo, weekFrom, weekTo, monthFrom, monthTo) =
            AssistantPeriodHelper.StandardBucketsUtc();
        using var connection = dbFactory.CreateConnection();

        async Task<decimal> SumPaidAsync(DateTime from, DateTime to) =>
            await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
                SELECT ISNULL(SUM(Amount), 0) FROM Payments
                WHERE TenantId = @TenantId AND Status = @Paid
                  AND PaymentDate >= @From AND PaymentDate < @To AND IsDeleted = 0
                """, new { context.TenantId, Paid = (int)PaymentStatus.Paid, From = from, To = to },
                cancellationToken: cancellationToken));

        var daily = await SumPaidAsync(todayFrom, todayTo);
        var weekly = await SumPaidAsync(weekFrom, weekTo);
        var monthly = await SumPaidAsync(monthFrom, monthTo);

        // Optional focused period when the question resolves outside the three standard buckets
        var (focusFrom, focusTo, focusLabel) = AssistantPeriodHelper.Resolve(null, args);
        decimal? focused = null;
        var showFocused = focusLabel is "previous_month" or "yesterday" or "last_30_days" or "custom";
        if (showFocused)
            focused = await SumPaidAsync(focusFrom, focusTo);

        if (daily == 0 && weekly == 0 && monthly == 0 && (focused is null or 0))
        {
            return new AiToolExecutionResult(
                Name, true,
                "No paid revenue recorded for today, the last 7 days, or this month (UTC). " +
                "Totals only include Payments with Status = Paid — incomplete payment data is not estimated.",
                new
                {
                    daily = new { start = todayFrom, end = todayTo, total = daily },
                    weekly = new { start = weekFrom, end = weekTo, total = weekly },
                    monthly = new { start = monthFrom, end = monthTo, total = monthly },
                    focused = focused is null ? null : new { start = focusFrom, end = focusTo, label = focusLabel, total = focused }
                });
        }

        var sb = new StringBuilder();
        sb.AppendLine("Revenue analytics (paid payments, UTC):");
        sb.AppendLine($"• Daily (today {todayFrom:yyyy-MM-dd}): {daily:N2}");
        sb.AppendLine($"• Weekly (last 7 days {weekFrom:yyyy-MM-dd} → {weekTo:yyyy-MM-dd}): {weekly:N2}");
        sb.AppendLine($"• Monthly ({monthFrom:yyyy-MM-dd} → {monthTo:yyyy-MM-dd}): {monthly:N2}");
        if (focused is not null)
            sb.AppendLine($"• Focused ({focusLabel} {focusFrom:yyyy-MM-dd} → {focusTo:yyyy-MM-dd}): {focused:N2}");

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new
            {
                daily = new { start = todayFrom, end = todayTo, total = daily },
                weekly = new { start = weekFrom, end = weekTo, total = weekly },
                monthly = new { start = monthFrom, end = monthTo, total = monthly },
                focused = focused is null ? null : new { start = focusFrom, end = focusTo, label = focusLabel, total = focused }
            });
    }
}

/// <summary>Batch 1 — vehicle with highest fuel liters consumed in the period.</summary>
public sealed class GetFuelUsageTopVehicleTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetFuelUsageTopVehicle";
    public string Description => "Identifies which vehicle consumed the most fuel (liters) in a reporting period.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "fuel usage", "consumed most fuel", "most fuel", "fuel consumption",
        "highest fuel usage", "which vehicle consumed", "liters used", "litres used"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        var rows = (await connection.QueryAsync<FuelUsageRow>(new CommandDefinition("""
            SELECT TOP 15
                v.Id AS VehicleId,
                v.RegistrationNumber AS PlateNumber,
                ISNULL(v.Name, v.RegistrationNumber) AS VehicleName,
                COUNT(*) AS EntryCount,
                ISNULL(SUM(f.Liters), 0) AS TotalLiters,
                ISNULL(SUM(f.TotalCost), 0) AS TotalCost
            FROM FuelLogs f
            INNER JOIN Vehicles v ON v.Id = f.VehicleId AND v.TenantId = f.TenantId AND v.IsDeleted = 0
            WHERE f.IsDeleted = 0
              AND f.TenantId = @TenantId
              AND f.FuelDate >= @From
              AND f.FuelDate < @To
            GROUP BY v.Id, v.RegistrationNumber, v.Name
            ORDER BY TotalLiters DESC, TotalCost DESC
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"No fuel usage entries for {from:yyyy-MM-dd} … {to:yyyy-MM-dd} (UTC). Cannot identify a top consumer without FuelLogs data.",
                new { period = new { start = from, end = to }, vehicles = rows });
        }

        var top = rows[0];
        var sb = new StringBuilder();
        sb.AppendLine(
            $"Highest fuel usage {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC): " +
            $"{top.VehicleName} ({top.PlateNumber}) with {top.TotalLiters:N1} L " +
            $"(cost {top.TotalCost:N2}, {top.EntryCount} entries).");
        if (rows.Count > 1)
        {
            sb.AppendLine("Next:");
            foreach (var r in rows.Skip(1).Take(4))
                sb.AppendLine($"• {r.VehicleName} ({r.PlateNumber}): {r.TotalLiters:N1} L");
        }

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new { period = new { start = from, end = to }, top, vehicles = rows });
    }

    private sealed record FuelUsageRow(
        int VehicleId,
        string PlateNumber,
        string VehicleName,
        int EntryCount,
        decimal TotalLiters,
        decimal TotalCost);
}

/// <summary>Batch 1 — best-performing driver by completed bookings then revenue.</summary>
public sealed class GetBestDriverPerformanceTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetBestDriverPerformance";
    public string Description => "Reports the best-performing driver by completed bookings and booking revenue for a period.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "best driver", "best performing driver", "top driver", "driver performance",
        "best performing", "who is the best driver", "top performing driver"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        var rows = (await connection.QueryAsync<DriverPerfRow>(new CommandDefinition("""
            SELECT TOP 15
                d.Id AS DriverId,
                d.FullName AS DriverName,
                COUNT(b.Id) AS TotalTrips,
                ISNULL(SUM(CASE WHEN b.Status = @Completed THEN 1 ELSE 0 END), 0) AS CompletedTrips,
                ISNULL(SUM(CASE WHEN b.Status = @Completed THEN b.TotalAmount ELSE 0 END), 0) AS TotalRevenue
            FROM Drivers d
            LEFT JOIN Bookings b ON b.DriverId = d.Id AND b.TenantId = @TenantId AND b.IsDeleted = 0
              AND b.CreatedAt >= @From AND b.CreatedAt < @To
            WHERE d.IsDeleted = 0 AND d.TenantId = @TenantId
            GROUP BY d.Id, d.FullName
            ORDER BY CompletedTrips DESC, TotalRevenue DESC, TotalTrips DESC
            """, new
        {
            context.TenantId,
            Completed = (int)BookingStatus.Completed,
            From = from,
            To = to
        }, cancellationToken: cancellationToken))).ToList();

        var withActivity = rows.Where(r => r.CompletedTrips > 0 || r.TotalTrips > 0).ToList();
        if (withActivity.Count == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"No driver booking activity for {from:yyyy-MM-dd} … {to:yyyy-MM-dd} (UTC). " +
                "Performance is ranked by completed bookings then booking revenue — GPS driver scores are not used here.",
                new { period = new { start = from, end = to }, drivers = rows });
        }

        var best = withActivity[0];
        var sb = new StringBuilder();
        sb.AppendLine(
            $"Best performing driver {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC): " +
            $"{best.DriverName} — {best.CompletedTrips} completed / {best.TotalTrips} bookings, revenue {best.TotalRevenue:N2}.");
        if (withActivity.Count > 1)
        {
            sb.AppendLine("Others:");
            foreach (var r in withActivity.Skip(1).Take(4))
                sb.AppendLine($"• {r.DriverName}: {r.CompletedTrips} completed, revenue {r.TotalRevenue:N2}");
        }

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new { period = new { start = from, end = to }, best, drivers = withActivity });
    }

    private sealed record DriverPerfRow(
        int DriverId,
        string DriverName,
        int TotalTrips,
        int CompletedTrips,
        decimal TotalRevenue);
}

/// <summary>
/// Batch 3 — idle approximation from fleet status snapshots + idle_vehicle alerts.
/// Full Traccar stop fan-out is not used here (too heavy / external); state that explicitly when empty.
/// </summary>
public sealed class GetIdleTimeReportTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetIdleTimeReport";
    public string Description =>
        "Reports fleet idle pressure from status snapshots and idle_vehicle alerts (not live Traccar stops).";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
        ["idle time", "idle report", "idling", "vehicle idle", "idle minutes", "most idle"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        var snap = await connection.QuerySingleOrDefaultAsync<IdleSnapAgg>(new CommandDefinition("""
            SELECT
                COUNT(*) AS SnapshotCount,
                ISNULL(SUM(Idle), 0) AS IdleSlots,
                ISNULL(SUM(Moving), 0) AS MovingSlots,
                ISNULL(SUM(TotalVehicles), 0) AS AvailableSlots
            FROM GpsFleetStatusSnapshots
            WHERE TenantId = @TenantId AND SnapshotAt >= @From AND SnapshotAt < @To
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken));

        var alerts = (await connection.QueryAsync<IdleAlertRow>(new CommandDefinition("""
            SELECT TOP 15
                v.Id AS VehicleId,
                v.RegistrationNumber AS PlateNumber,
                ISNULL(v.Name, v.RegistrationNumber) AS VehicleName,
                COUNT(*) AS AlertCount
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.IsDeleted = 0 AND v.TenantId = @TenantId
            WHERE e.IsDeleted = 0
              AND e.EventType = N'idle_vehicle'
              AND e.Timestamp >= @From AND e.Timestamp < @To
            GROUP BY v.Id, v.RegistrationNumber, v.Name
            ORDER BY AlertCount DESC
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        var snapshotCount = snap?.SnapshotCount ?? 0;
        var idleSlots = snap?.IdleSlots ?? 0;
        var idlePct = snap is { AvailableSlots: > 0 }
            ? Math.Round(snap.IdleSlots * 100m / snap.AvailableSlots, 1)
            : (decimal?)null;

        if (snapshotCount == 0 && alerts.Count == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"Idle time report unavailable for {from:yyyy-MM-dd} … {to:yyyy-MM-dd} (UTC): " +
                "no GpsFleetStatusSnapshots and no idle_vehicle alerts. " +
                "Detailed stop-level idle (Traccar) is not queried by the assistant — use Fleet Idle Report for that.",
                new { period = new { start = from, end = to }, snapshotCount, idleSlots, idlePct, alerts });
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Idle time report {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC):");
        if (snapshotCount > 0)
            sb.AppendLine(
                $"• Snapshot idle pressure: {idlePct}% idle slots ({idleSlots:N0} idle / {snap!.AvailableSlots:N0} available across {snapshotCount} snapshot(s)).");
        else
            sb.AppendLine("• No fleet status snapshots in period — idle % not computed.");

        if (alerts.Count == 0)
            sb.AppendLine("• No idle_vehicle alert events in period.");
        else
        {
            sb.AppendLine("Top idle_vehicle alert vehicles:");
            foreach (var a in alerts.Take(8))
                sb.AppendLine($"• {a.VehicleName} ({a.PlateNumber}): {a.AlertCount} alert(s)");
        }

        sb.AppendLine("Note: this is a local approximation — not Traccar stop-duration idle.");

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new { period = new { start = from, end = to }, snapshotCount, idleSlots, idlePct, movingSlots = snap?.MovingSlots, alerts });
    }

    private sealed record IdleSnapAgg(int SnapshotCount, int IdleSlots, int MovingSlots, int AvailableSlots);
    private sealed record IdleAlertRow(int VehicleId, string PlateNumber, string VehicleName, int AlertCount);
}

/// <summary>Batch 3 — SOS / panic events from GpsAlertEvents.</summary>
public sealed class GetSosEventsSummaryTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetSosEventsSummary";
    public string Description => "Lists and summarizes SOS / panic GPS alert events for a period.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
        ["sos", "panic", "emergency alert", "sos events", "sos summary"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        var rows = (await connection.QueryAsync<SosRow>(new CommandDefinition("""
            SELECT TOP 25
                e.Id,
                e.EventType,
                e.Severity,
                e.Timestamp,
                e.IsAcknowledged,
                e.Status,
                v.Id AS VehicleId,
                v.RegistrationNumber AS PlateNumber,
                ISNULL(v.Name, v.RegistrationNumber) AS VehicleName,
                e.DriverId
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.IsDeleted = 0 AND v.TenantId = @TenantId
            WHERE e.IsDeleted = 0
              AND e.Timestamp >= @From AND e.Timestamp < @To
              AND (
                    e.EventType IN (N'sos', N'panic')
                 OR LOWER(e.EventType) LIKE N'%sos%'
                 OR LOWER(e.EventType) LIKE N'%panic%'
              )
            ORDER BY e.Timestamp DESC
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"No SOS/panic events for {from:yyyy-MM-dd} … {to:yyyy-MM-dd} (UTC).",
                new { period = new { start = from, end = to }, events = rows });
        }

        var open = rows.Count(r => !r.IsAcknowledged);
        var sb = new StringBuilder();
        sb.AppendLine($"SOS events {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC): {rows.Count} event(s), {open} unacknowledged.");
        foreach (var r in rows.Take(15))
            sb.AppendLine(
                $"• #{r.Id} {r.EventType}/{r.Severity} — {r.VehicleName} ({r.PlateNumber}) " +
                $"@ {r.Timestamp:u}, ack={(r.IsAcknowledged ? "yes" : "no")}, status={r.Status ?? "n/a"}");

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new { period = new { start = from, end = to }, total = rows.Count, unacknowledged = open, events = rows });
    }

    private sealed record SosRow(
        int Id,
        string EventType,
        string? Severity,
        DateTime Timestamp,
        bool IsAcknowledged,
        string? Status,
        int VehicleId,
        string PlateNumber,
        string VehicleName,
        int? DriverId);
}

/// <summary>Batch 3 — expense totals from FleetExpenses, TripExpenses, fuel, and maintenance.</summary>
public sealed class GetExpenseTrackingTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetExpenseTracking";
    public string Description =>
        "Summarizes expenses by category for a period (FleetExpenses, trip expenses, fuel, maintenance).";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
        ["expense", "expenses", "expense tracking", "spending", "expenditure by category", "cost breakdown"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        var fuel = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT ISNULL(SUM(TotalCost), 0) FROM FuelLogs
            WHERE TenantId = @TenantId AND FuelDate >= @From AND FuelDate < @To AND IsDeleted = 0
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken));

        var maintenance = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT ISNULL(SUM(Cost), 0) FROM Maintenance
            WHERE TenantId = @TenantId AND MaintenanceDate >= @From AND MaintenanceDate < @To AND IsDeleted = 0
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken));

        var tripExpenses = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT ISNULL(SUM(e.Amount), 0)
            FROM TripExpenses e
            INNER JOIN Trips t ON t.Id = e.TripId AND t.IsDeleted = 0 AND t.TenantId = @TenantId
            WHERE e.IsDeleted = 0 AND e.ExpenseDate >= @From AND e.ExpenseDate < @To
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken));

        decimal fleetExpenses = 0;
        IReadOnlyList<FleetCatRow> byCategory = Array.Empty<FleetCatRow>();
        var hasFleetExpenses = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(N'dbo.FleetExpenses', N'U') IS NULL THEN 0 ELSE 1 END
            """, cancellationToken: cancellationToken));

        if (hasFleetExpenses == 1)
        {
            fleetExpenses = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
                SELECT ISNULL(SUM(Amount), 0) FROM FleetExpenses
                WHERE IsDeleted = 0 AND TenantId = @TenantId
                  AND ExpenseDate >= @From AND ExpenseDate < @To
                """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken));

            byCategory = (await connection.QueryAsync<FleetCatRow>(new CommandDefinition("""
                SELECT TOP 20 Category, COUNT(*) AS EntryCount, ISNULL(SUM(Amount), 0) AS TotalAmount
                FROM FleetExpenses
                WHERE IsDeleted = 0 AND TenantId = @TenantId
                  AND ExpenseDate >= @From AND ExpenseDate < @To
                GROUP BY Category
                ORDER BY TotalAmount DESC
                """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();
        }

        var grandTotal = fuel + maintenance + tripExpenses + fleetExpenses;
        if (grandTotal == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"No expenses recorded for {from:yyyy-MM-dd} … {to:yyyy-MM-dd} (UTC) " +
                "(fuel, maintenance, trip expenses, fleet expenses).",
                new
                {
                    period = new { start = from, end = to },
                    fuel,
                    maintenance,
                    tripExpenses,
                    fleetExpenses,
                    byCategory,
                    grandTotal
                });
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Expense tracking {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC): total {grandTotal:N2}");
        sb.AppendLine($"• Fuel: {fuel:N2}");
        sb.AppendLine($"• Maintenance: {maintenance:N2}");
        sb.AppendLine($"• Trip expenses: {tripExpenses:N2}");
        sb.AppendLine($"• Fleet expenses: {fleetExpenses:N2}" + (hasFleetExpenses == 0 ? " (table not present)" : ""));
        if (byCategory.Count > 0)
        {
            sb.AppendLine("Fleet expense categories:");
            foreach (var c in byCategory.Take(10))
                sb.AppendLine($"• {c.Category}: {c.TotalAmount:N2} ({c.EntryCount} entries)");
        }

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new
            {
                period = new { start = from, end = to },
                fuel,
                maintenance,
                tripExpenses,
                fleetExpenses,
                byCategory,
                grandTotal
            });
    }

    private sealed record FleetCatRow(string Category, int EntryCount, decimal TotalAmount);
}

/// <summary>
/// Batch 3 — platform/super-admin only cross-tenant comparison.
/// Must refuse when <see cref="AiToolExecutionContext.IsPlatformSuperAdmin"/> is false.
/// </summary>
public sealed class GetTenantComparisonTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetTenantComparison";
    public string Description =>
        "Super-admin only: compares revenue, trips, vehicles, and open smart alerts across tenants.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "tenant comparison", "compare tenants", "across tenants", "all tenants",
        "cross-tenant", "platform comparison", "super admin tenants"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        if (!context.IsPlatformSuperAdmin)
        {
            return new AiToolExecutionResult(
                Name, false, string.Empty,
                Error: "Tenant comparison is restricted to platform super-admins. Cross-tenant data is not available.");
        }

        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        var rows = (await connection.QueryAsync<TenantCmpRow>(new CommandDefinition("""
            SELECT TOP 50
                t.Id AS TenantId,
                t.Name AS TenantName,
                (SELECT COUNT(*) FROM Vehicles v WHERE v.TenantId = t.Id AND v.IsDeleted = 0) AS VehicleCount,
                (SELECT ISNULL(SUM(p.Amount), 0) FROM Payments p
                 WHERE p.TenantId = t.Id AND p.IsDeleted = 0 AND p.Status = @Paid
                   AND p.PaymentDate >= @From AND p.PaymentDate < @To) AS Revenue,
                (SELECT COUNT(*) FROM Trips tr
                 WHERE tr.TenantId = t.Id AND tr.IsDeleted = 0
                   AND tr.TripDate >= @From AND tr.TripDate < @To) AS TripCount,
                (SELECT COUNT(*) FROM SmartAlerts sa
                 WHERE sa.TenantId = t.Id AND sa.Status IN (N'New', N'Acknowledged')) AS OpenSmartAlerts
            FROM Tenants t
            WHERE t.IsActive = 1
            ORDER BY Revenue DESC, TripCount DESC
            """, new { Paid = (int)PaymentStatus.Paid, From = from, To = to },
            cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                "No tenants available for comparison.",
                new { period = new { start = from, end = to }, tenants = rows });
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Tenant comparison {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC) — {rows.Count} tenant(s):");
        foreach (var r in rows.Take(20))
            sb.AppendLine(
                $"• {r.TenantName} (#{r.TenantId}): revenue {r.Revenue:N2}, trips {r.TripCount}, " +
                $"vehicles {r.VehicleCount}, open smart alerts {r.OpenSmartAlerts}");

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new { period = new { start = from, end = to }, tenants = rows });
    }

    private sealed record TenantCmpRow(
        int TenantId,
        string TenantName,
        int VehicleCount,
        decimal Revenue,
        int TripCount,
        int OpenSmartAlerts);
}

/// <summary>Batch 3 — peak booking / trip hours from CreatedAt / TripDate hour buckets.</summary>
public sealed class GetPeakHoursAnalysisTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetPeakHoursAnalysis";
    public string Description =>
        "Identifies peak booking and trip hours (UTC) so operations can plan coverage.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
        ["peak hours", "peak hour", "busiest hour", "busy hours", "when are bookings", "dispatch peak"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        using var connection = dbFactory.CreateConnection();

        var bookingHours = (await connection.QueryAsync<HourBucket>(new CommandDefinition("""
            SELECT DATEPART(HOUR, CreatedAt) AS HourUtc, COUNT(*) AS Cnt
            FROM Bookings
            WHERE IsDeleted = 0 AND TenantId = @TenantId
              AND CreatedAt >= @From AND CreatedAt < @To
            GROUP BY DATEPART(HOUR, CreatedAt)
            ORDER BY Cnt DESC
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        var tripHours = (await connection.QueryAsync<HourBucket>(new CommandDefinition("""
            SELECT DATEPART(HOUR, TripDate) AS HourUtc, COUNT(*) AS Cnt
            FROM Trips
            WHERE IsDeleted = 0 AND TenantId = @TenantId
              AND TripDate >= @From AND TripDate < @To
            GROUP BY DATEPART(HOUR, TripDate)
            ORDER BY Cnt DESC
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        if (bookingHours.Count == 0 && tripHours.Count == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"No booking or trip activity for peak-hours analysis in {from:yyyy-MM-dd} … {to:yyyy-MM-dd} (UTC).",
                new { period = new { start = from, end = to }, bookingHours, tripHours });
        }

        static string FormatTop(IReadOnlyList<HourBucket> buckets, int take) =>
            string.Join(", ", buckets.Take(take).Select(b => $"{b.HourUtc:00}:00 ({b.Cnt})"));

        var sb = new StringBuilder();
        sb.AppendLine($"Peak hours analysis {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC hours):");
        if (bookingHours.Count > 0)
            sb.AppendLine($"• Busiest booking hours: {FormatTop(bookingHours, 5)}");
        else
            sb.AppendLine("• No bookings in period.");
        if (tripHours.Count > 0)
            sb.AppendLine($"• Busiest trip hours: {FormatTop(tripHours, 5)}");
        else
            sb.AppendLine("• No trips in period.");

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new
            {
                period = new { start = from, end = to },
                bookingHours,
                tripHours,
                peakBookingHour = bookingHours.FirstOrDefault()?.HourUtc,
                peakTripHour = tripHours.FirstOrDefault()?.HourUtc
            });
    }

    private sealed record HourBucket(int HourUtc, int Cnt);
}
