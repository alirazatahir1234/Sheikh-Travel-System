using System.Text;
using System.Text.Json;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.AI;
using SheikhTravelSystem.Application.Features.MaintenanceModule;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Tools;

/// <summary>
/// Phase 3 — Predictive Intelligence allowlisted tools (rules/stats only; no ML claims).
/// Kept in a separate file so Batch 1–3 reporting WIP is not clobbered.
/// </summary>
public sealed class GetMaintenanceRiskForecastTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetMaintenanceRiskForecast";
    public string Description =>
        "Rule-based maintenance risk: oil / tires / engine life with evidence. " +
        "Complements Smart Ops MaintenanceDue alerts (does not re-raise them).";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "maintenance risk", "predictive maintenance", "which vehicles may need maintenance",
        "service risk", "oil change due", "tire wear", "engine life", "maintenance soon",
        "vehicles at risk of maintenance", "forecast maintenance"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var asOf = DateTime.UtcNow;

        // Open Smart Ops alerts — surface as context; do not invent a second scanner conflict.
        var smartAlerts = (await connection.QueryAsync<SmartMaintRow>(new CommandDefinition("""
            SELECT TOP 25 Id, Severity, Message, Status, RelatedEntityId, CreatedAt
            FROM SmartAlerts
            WHERE TenantId = @TenantId
              AND RuleType = N'MaintenanceDue'
              AND Status IN (N'New', N'Acknowledged')
            ORDER BY
                CASE Severity WHEN N'Critical' THEN 0 WHEN N'Warning' THEN 1 ELSE 2 END,
                CreatedAt DESC
            """, new { context.TenantId }, cancellationToken: cancellationToken))).ToList();

        var rows = (await connection.QueryAsync<VehiclePredRow>(new CommandDefinition("""
            SELECT v.Id AS VehicleId,
                   ISNULL(v.Name, v.RegistrationNumber) AS VehicleName,
                   v.RegistrationNumber AS VehicleRegistration,
                   v.CurrentMileage, v.Year, v.PurchaseDate,
                   v.OilIntervalKm, v.OilIntervalDays, v.LastOilChangeDate, v.LastOilChangeMileage,
                   v.TireInstallDate, v.TireInstallMileage, v.TireExpectedLifeKm,
                   v.EngineExpectedLifeKm, v.EngineExpectedLifeYears,
                   oil.IntervalKm AS ScheduleOilIntervalKm,
                   oil.IntervalDays AS ScheduleOilIntervalDays,
                   oil.LastServiceDate AS ScheduleLastOilDate,
                   oil.LastServiceMileage AS ScheduleLastOilMileage,
                   tire.LastServiceDate AS ScheduleTireInstallDate,
                   tire.LastServiceMileage AS ScheduleTireInstallMileage,
                   fuel.AvgDailyKm
            FROM Vehicles v
            OUTER APPLY (
                SELECT TOP 1
                    CASE WHEN s.IntervalType = N'Mileage' THEN CAST(s.IntervalValue AS DECIMAL(12,2)) END AS IntervalKm,
                    CASE
                        WHEN s.IntervalType IN (N'Days', N'Day') THEN s.IntervalValue
                        WHEN s.IntervalType IN (N'Months', N'Month') THEN s.IntervalValue * 30
                    END AS IntervalDays,
                    s.LastServiceDate, s.LastServiceMileage
                FROM VehicleMaintenanceSchedules s
                WHERE s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                  AND (s.ServiceTypeName LIKE N'%oil%' OR s.ServiceTypeName LIKE N'%Oil%')
                ORDER BY s.UpdatedAt DESC, s.Id DESC
            ) oil
            OUTER APPLY (
                SELECT TOP 1 s.LastServiceDate, s.LastServiceMileage
                FROM VehicleMaintenanceSchedules s
                WHERE s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                  AND (s.ServiceTypeName LIKE N'%tire%' OR s.ServiceTypeName LIKE N'%Tire%'
                       OR s.ServiceTypeName LIKE N'%tyre%' OR s.ServiceTypeName LIKE N'%Tyre%')
                ORDER BY s.UpdatedAt DESC, s.Id DESC
            ) tire
            OUTER APPLY (
                SELECT CASE
                    WHEN COUNT(*) >= 2 AND DATEDIFF(DAY, MIN(f.FuelDate), MAX(f.FuelDate)) >= 7
                         AND MAX(f.OdometerReading) > MIN(f.OdometerReading)
                    THEN CAST(MAX(f.OdometerReading) - MIN(f.OdometerReading) AS DECIMAL(12,2))
                         / NULLIF(DATEDIFF(DAY, MIN(f.FuelDate), MAX(f.FuelDate)), 0)
                END AS AvgDailyKm
                FROM FuelLogs f
                WHERE f.VehicleId = v.Id AND f.TenantId = @TenantId AND f.IsDeleted = 0
                  AND f.FuelDate >= DATEADD(DAY, -90, GETUTCDATE())
            ) fuel
            WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId
              AND v.Status <> @Retired
            """, new
        {
            context.TenantId,
            Retired = (int)VehicleStatus.Retired
        }, cancellationToken: cancellationToken))).ToList();

        var inputs = rows.Select(r => new PredictiveVehicleInput(
            r.VehicleId,
            r.VehicleName ?? $"#{r.VehicleId}",
            r.VehicleRegistration,
            r.CurrentMileage,
            r.Year,
            r.PurchaseDate,
            r.OilIntervalKm,
            r.OilIntervalDays,
            r.LastOilChangeDate,
            r.LastOilChangeMileage,
            r.TireInstallDate,
            r.TireInstallMileage,
            r.TireExpectedLifeKm,
            r.EngineExpectedLifeKm,
            r.EngineExpectedLifeYears,
            r.ScheduleOilIntervalKm,
            r.ScheduleOilIntervalDays,
            r.ScheduleLastOilDate,
            r.ScheduleLastOilMileage,
            r.ScheduleTireInstallDate,
            r.ScheduleTireInstallMileage,
            r.AvgDailyKm)).ToList();

        var predictive = PredictiveMaintenanceHelper.ComputeAll(inputs, asOf)
            .Where(a => a.Status is "Overdue" or "DueSoon" or "NearEndOfLife" or "Aging")
            .Take(25)
            .ToList();

        if (predictive.Count == 0 && smartAlerts.Count == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                "No elevated maintenance risk from rule-based oil/tire/engine checks, " +
                "and no open Smart Ops MaintenanceDue alerts. " +
                "Predictions use service intervals, odometer, and schedules — not machine learning.",
                new
                {
                    method = "rules",
                    smartOpsMaintenanceDue = smartAlerts,
                    risks = predictive,
                    vehicleCountEvaluated = rows.Count
                });
        }

        var sb = new StringBuilder();
        sb.AppendLine(
            $"Maintenance risk (rules-based, as of {asOf:yyyy-MM-dd} UTC): " +
            $"{predictive.Count} predictive flag(s), {smartAlerts.Count} open Smart Ops MaintenanceDue alert(s). " +
            "Categories: OilChange / TireWear / EngineLife. Not ML.");
        if (predictive.Count > 0)
        {
            sb.AppendLine("Predictive (transparent evidence):");
            foreach (var a in predictive.Take(12))
                sb.AppendLine($"• [{a.Severity}/{a.Category}] {a.Message}");
        }

        if (smartAlerts.Count > 0)
        {
            sb.AppendLine("Smart Ops schedule alerts (already raised — not duplicated):");
            foreach (var a in smartAlerts.Take(8))
                sb.AppendLine($"• [{a.Severity}/{a.Status}] {a.Message}");
        }

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new
            {
                method = "rules",
                asOf,
                smartOpsMaintenanceDue = smartAlerts,
                risks = predictive,
                vehicleCountEvaluated = rows.Count
            });
    }

    private sealed record SmartMaintRow(
        long Id, string Severity, string Message, string Status, int? RelatedEntityId, DateTime CreatedAt);

    private sealed class VehiclePredRow
    {
        public int VehicleId { get; init; }
        public string? VehicleName { get; init; }
        public string? VehicleRegistration { get; init; }
        public decimal CurrentMileage { get; init; }
        public int? Year { get; init; }
        public DateTime? PurchaseDate { get; init; }
        public decimal? OilIntervalKm { get; init; }
        public int? OilIntervalDays { get; init; }
        public DateTime? LastOilChangeDate { get; init; }
        public decimal? LastOilChangeMileage { get; init; }
        public DateTime? TireInstallDate { get; init; }
        public decimal? TireInstallMileage { get; init; }
        public decimal? TireExpectedLifeKm { get; init; }
        public decimal? EngineExpectedLifeKm { get; init; }
        public int? EngineExpectedLifeYears { get; init; }
        public decimal? ScheduleOilIntervalKm { get; init; }
        public int? ScheduleOilIntervalDays { get; init; }
        public DateTime? ScheduleLastOilDate { get; init; }
        public decimal? ScheduleLastOilMileage { get; init; }
        public DateTime? ScheduleTireInstallDate { get; init; }
        public decimal? ScheduleTireInstallMileage { get; init; }
        public decimal? AvgDailyKm { get; init; }
    }
}

public sealed class GetTripDelayRiskTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetTripDelayRisk";
    public string Description =>
        "Flags trips at risk of late arrival from schedule + optional GPS staleness. " +
        "No external Maps ETA calls.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "trip delay", "delay risk", "at risk of delay", "risk of delay", "late arrival",
        "trips at risk", "running late", "delayed trips", "which trips are late",
        "eta risk", "stale gps trip"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var asOf = DateTime.UtcNow;

        var rows = (await connection.QueryAsync<TripRiskRow>(new CommandDefinition("""
            SELECT TOP 80
                t.Id AS TripId,
                t.TripNumber,
                t.TripName,
                t.Status,
                t.PlannedStart,
                t.PlannedEnd,
                t.EstimatedDurationMinutes,
                t.VehicleId,
                ISNULL(v.Name, v.RegistrationNumber) AS VehicleName,
                vcl.LastUpdate AS LastGpsUtc
            FROM Trips t
            LEFT JOIN Vehicles v ON v.Id = t.VehicleId AND v.IsDeleted = 0 AND v.TenantId = t.TenantId
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = t.VehicleId
            WHERE t.IsDeleted = 0
              AND t.TenantId = @TenantId
              AND t.Status NOT IN (@Completed, @Cancelled, @Failed, @Draft)
              AND t.PlannedStart >= DATEADD(DAY, -1, @AsOf)
              AND t.PlannedStart < DATEADD(DAY, 2, @AsOf)
            ORDER BY t.PlannedStart ASC
            """, new
        {
            context.TenantId,
            AsOf = asOf,
            Completed = (int)TripStatus.Completed,
            Cancelled = (int)TripStatus.Cancelled,
            Failed = (int)TripStatus.Failed,
            Draft = (int)TripStatus.Draft
        }, cancellationToken: cancellationToken))).ToList();

        var assessments = new List<object>();
        var atRisk = new List<(TripRiskRow Row, TripDelayAssessment A)>();
        var staleOnly = new List<(TripRiskRow Row, TripDelayAssessment A)>();

        foreach (var row in rows)
        {
            var assessment = PredictiveIntelligenceRuleLogic.EvaluateTripDelay(
                (TripStatus)row.Status,
                row.PlannedStart,
                row.PlannedEnd,
                row.EstimatedDurationMinutes,
                row.LastGpsUtc,
                asOf);

            if (assessment.IsAtRisk)
                atRisk.Add((row, assessment));
            else if (assessment.GpsStale && PredictiveIntelligenceRuleLogic.IsActiveTripStatus((TripStatus)row.Status))
                staleOnly.Add((row, assessment));

            if (assessment.IsAtRisk || (assessment.GpsStale && PredictiveIntelligenceRuleLogic.IsActiveTripStatus((TripStatus)row.Status)))
            {
                assessments.Add(new
                {
                    row.TripId,
                    row.TripNumber,
                    row.TripName,
                    status = ((TripStatus)row.Status).ToString(),
                    row.VehicleId,
                    row.VehicleName,
                    risk = assessment.Kind.ToString(),
                    gpsStale = assessment.GpsStale,
                    expectedEnd = assessment.ExpectedEnd,
                    evidence = assessment.Evidence,
                    lastGpsUtc = row.LastGpsUtc
                });
            }
        }

        if (atRisk.Count == 0 && staleOnly.Count == 0)
        {
            return new AiToolExecutionResult(
                Name, true,
                rows.Count == 0
                    ? "No active or upcoming trips in the ±1–2 day window to assess for delay risk."
                    : $"Evaluated {rows.Count} trip(s): none currently at schedule-based delay risk. " +
                      "Assessment uses PlannedStart/PlannedEnd/EstimatedDurationMinutes only — no Maps ETA.",
                new
                {
                    method = "schedule_rules",
                    asOf,
                    evaluated = rows.Count,
                    atRisk = assessments
                });
        }

        var sb = new StringBuilder();
        sb.AppendLine(
            $"Trip delay risk (schedule rules, {asOf:u} UTC): {atRisk.Count} at risk, " +
            $"{staleOnly.Count} active with stale GPS only. No external Maps calls.");
        foreach (var (row, a) in atRisk.Take(15))
        {
            var gpsNote = a.GpsStale ? " [GPS stale — location uncertain]" : "";
            sb.AppendLine(
                $"• {row.TripNumber} ({row.VehicleName ?? "no vehicle"}) — {a.Kind}: {a.Evidence}{gpsNote}");
        }

        if (staleOnly.Count > 0)
        {
            sb.AppendLine("Stale GPS on active trips (flagged separately; delay not confirmed):");
            foreach (var (row, a) in staleOnly.Take(8))
                sb.AppendLine(
                    $"• {row.TripNumber} — last GPS {(row.LastGpsUtc?.ToString("u") ?? "never")}; {a.Evidence}");
        }

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new
            {
                method = "schedule_rules",
                asOf,
                evaluated = rows.Count,
                atRiskCount = atRisk.Count,
                staleGpsOnlyCount = staleOnly.Count,
                atRisk = assessments
            });
    }

    private sealed record TripRiskRow(
        int TripId,
        string TripNumber,
        string? TripName,
        int Status,
        DateTime PlannedStart,
        DateTime? PlannedEnd,
        int? EstimatedDurationMinutes,
        int? VehicleId,
        string? VehicleName,
        DateTime? LastGpsUtc);
}

public sealed class GetFuelAnomalyInvestigationTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetFuelAnomalyInvestigation";
    public string Description =>
        "Surfaces Smart Ops fuel anomalies plus unusual L/100 km vs fleet median when enough FuelLogs exist. " +
        "Investigation wording only — not misconduct.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "fuel anomaly", "unusual fuel", "fuel investigation", "fuel spike", "suspicious fuel",
        "fuel baseline", "high fuel consumption anomaly", "fuel outlier", "liters per 100"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (from, to, _) = AssistantPeriodHelper.Resolve(null, args);
        // Prefer at least 30 days for baseline quality when period is short default month
        if ((to - from).TotalDays < 14)
        {
            to = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc);
            from = to.AddDays(-30);
        }

        using var connection = dbFactory.CreateConnection();

        var smartAlerts = (await connection.QueryAsync<FuelSmartRow>(new CommandDefinition("""
            SELECT TOP 25 Id, Severity, Message, Status, RelatedEntityId, CreatedAt
            FROM SmartAlerts
            WHERE TenantId = @TenantId
              AND RuleType = N'FuelAnomaly'
              AND Status IN (N'New', N'Acknowledged')
            ORDER BY CreatedAt DESC
            """, new { context.TenantId }, cancellationToken: cancellationToken))).ToList();

        var vehicleStats = (await connection.QueryAsync<FuelStatRow>(new CommandDefinition("""
            SELECT
                v.Id AS VehicleId,
                ISNULL(v.Name, v.RegistrationNumber) AS VehicleName,
                v.RegistrationNumber AS PlateNumber,
                COUNT(*) AS EntryCount,
                ISNULL(SUM(f.Liters), 0) AS TotalLiters,
                ISNULL(SUM(f.TotalCost), 0) AS TotalCost,
                MIN(f.OdometerReading) AS MinOdo,
                MAX(f.OdometerReading) AS MaxOdo,
                MIN(f.FuelDate) AS FirstFuel,
                MAX(f.FuelDate) AS LastFuel
            FROM FuelLogs f
            INNER JOIN Vehicles v ON v.Id = f.VehicleId AND v.TenantId = f.TenantId AND v.IsDeleted = 0
            WHERE f.IsDeleted = 0
              AND f.TenantId = @TenantId
              AND f.FuelDate >= @From
              AND f.FuelDate < @To
            GROUP BY v.Id, v.Name, v.RegistrationNumber
            """, new { context.TenantId, From = from, To = to }, cancellationToken: cancellationToken))).ToList();

        var rates = new List<(FuelStatRow Row, decimal Rate, int SpanDays, decimal Km)>();
        foreach (var s in vehicleStats)
        {
            var km = s.MaxOdo - s.MinOdo;
            var spanDays = Math.Max(0, (int)(s.LastFuel.Date - s.FirstFuel.Date).TotalDays);
            if (!PredictiveIntelligenceRuleLogic.HasSufficientFuelSample(s.EntryCount, spanDays, km))
                continue;
            var rate = PredictiveIntelligenceRuleLogic.ComputeLitersPer100Km(s.TotalLiters, s.MinOdo, s.MaxOdo);
            if (rate is null) continue;
            rates.Add((s, rate.Value, spanDays, km));
        }

        var median = PredictiveIntelligenceRuleLogic.Median(rates.Select(r => r.Rate).ToList());
        var outliers = new List<object>();
        var outlierSummaries = new List<string>();

        if (median is not null && rates.Count >= PredictiveIntelligenceRuleLogic.MinFleetVehiclesForMedian)
        {
            foreach (var (row, rate, spanDays, km) in rates)
            {
                if (!PredictiveIntelligenceRuleLogic.IsFuelRateAnomaly(rate, median.Value))
                    continue;
                var wording = PredictiveIntelligenceRuleLogic.FuelInvestigationWording(
                    $"{row.VehicleName} ({row.PlateNumber})", rate, median.Value, row.EntryCount);
                outlierSummaries.Add(wording);
                outliers.Add(new
                {
                    row.VehicleId,
                    row.VehicleName,
                    row.PlateNumber,
                    litersPer100Km = rate,
                    fleetMedian = median,
                    row.EntryCount,
                    spanDays,
                    distanceKm = km,
                    row.TotalLiters,
                    row.TotalCost
                });
            }
        }

        var insufficientNote = vehicleStats.Count > 0 && rates.Count < PredictiveIntelligenceRuleLogic.MinFleetVehiclesForMedian
            ? $" L/100 km baseline needs ≥{PredictiveIntelligenceRuleLogic.MinFleetVehiclesForMedian} vehicles with " +
              $"≥{PredictiveIntelligenceRuleLogic.MinFuelEntriesForBaseline} fills, " +
              $"≥{PredictiveIntelligenceRuleLogic.MinSpanDaysForFuelBaseline} days, " +
              $"≥{PredictiveIntelligenceRuleLogic.MinKmForFuelRate} km — currently {rates.Count} qualified."
            : null;

        if (smartAlerts.Count == 0 && outliers.Count == 0)
        {
            var msg = new StringBuilder();
            msg.Append(
                $"No open Smart Ops FuelAnomaly alerts and no L/100 km outliers for {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC).");
            if (insufficientNote is not null)
                msg.Append(insufficientNote);
            msg.Append(" Wording is investigation-only; not misconduct.");

            return new AiToolExecutionResult(
                Name, true, msg.ToString(),
                new
                {
                    method = "smart_ops_plus_l100km_rules",
                    period = new { start = from, end = to },
                    smartOpsFuelAnomalies = smartAlerts,
                    fleetMedianLitersPer100Km = median,
                    qualifiedVehicleRates = rates.Count,
                    outliers,
                    note = insufficientNote
                });
        }

        var sb = new StringBuilder();
        sb.AppendLine(
            $"Fuel anomaly investigation {from:yyyy-MM-dd} → {to:yyyy-MM-dd} (UTC): " +
            $"{smartAlerts.Count} Smart Ops alert(s), {outliers.Count} L/100 km outlier(s)" +
            (median is null ? "." : $" (fleet median {median:N1} L/100 km).") +
            " Not proof of misconduct.");

        if (smartAlerts.Count > 0)
        {
            sb.AppendLine("Smart Ops (capacity / duplicate-window):");
            foreach (var a in smartAlerts.Take(10))
                sb.AppendLine($"• [{a.Severity}/{a.Status}] {a.Message}");
        }

        if (outlierSummaries.Count > 0)
        {
            sb.AppendLine("Consumption vs fleet median:");
            foreach (var line in outlierSummaries.Take(10))
                sb.AppendLine($"• {line}");
        }

        if (insufficientNote is not null)
            sb.AppendLine(insufficientNote.Trim());

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new
            {
                method = "smart_ops_plus_l100km_rules",
                period = new { start = from, end = to },
                smartOpsFuelAnomalies = smartAlerts,
                fleetMedianLitersPer100Km = median,
                qualifiedVehicleRates = rates.Count,
                outliers
            });
    }

    private sealed record FuelSmartRow(
        long Id, string Severity, string Message, string Status, int? RelatedEntityId, DateTime CreatedAt);

    private sealed record FuelStatRow(
        int VehicleId,
        string VehicleName,
        string PlateNumber,
        int EntryCount,
        decimal TotalLiters,
        decimal TotalCost,
        decimal MinOdo,
        decimal MaxOdo,
        DateTime FirstFuel,
        DateTime LastFuel);
}
