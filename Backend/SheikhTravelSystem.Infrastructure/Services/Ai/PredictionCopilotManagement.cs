using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ai;

public sealed class AiPredictionService(
    IDbConnectionFactory dbFactory,
    IAiManagementService aiManagement,
    ILogger<AiPredictionService> logger) : IAiPredictionService
{
    public async Task CaptureFeaturesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var vehicles = await connection.QueryAsync<(int Id, decimal? Odometer, decimal? Battery)>(new CommandDefinition("""
            SELECT TOP 200 v.Id,
                   (SELECT TOP 1 OdometerReading FROM Maintenance m WHERE m.VehicleId = v.Id AND m.IsDeleted = 0 ORDER BY m.Id DESC) AS Odometer,
                   vcl.BatteryLevel AS Battery
            FROM Vehicles v
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        foreach (var v in vehicles)
        {
            var maintCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Maintenance WHERE VehicleId = @Id AND IsDeleted = 0",
                new { v.Id }, cancellationToken: cancellationToken));

            var features = JsonSerializer.Serialize(new
            {
                odometer = v.Odometer,
                battery = v.Battery,
                maintenanceRecords = maintCount,
                capturedAt = DateTime.UtcNow
            });

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO AiFeatureSnapshots (TenantId, EntityType, EntityId, FeatureSet, FeaturesJson, CapturedAt)
                VALUES (@TenantId, 'Vehicle', @EntityId, 'maintenance_v1', @FeaturesJson, GETUTCDATE())
                """,
                new { TenantId = tenantId, EntityId = v.Id, FeaturesJson = features },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation("Captured feature snapshots for tenant {TenantId}", tenantId);
    }

    public async Task RunHeuristicPredictionsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var config = await aiManagement.GetConfigAsync(tenantId, cancellationToken);
        if (!config.PredictionsEnabled) return;

        using var connection = dbFactory.CreateConnection();

        // Predictive maintenance heuristic: overdue + high odometer + low battery
        var candidates = await connection.QueryAsync<(int VehicleId, decimal? Odometer, decimal? Battery, DateTime? NextDue)>(new CommandDefinition("""
            SELECT v.Id AS VehicleId,
                   (SELECT TOP 1 OdometerReading FROM Maintenance m WHERE m.VehicleId = v.Id AND m.IsDeleted = 0 ORDER BY m.Id DESC) AS Odometer,
                   vcl.BatteryLevel AS Battery,
                   (SELECT MIN(NextDueDate) FROM Maintenance m WHERE m.VehicleId = v.Id AND m.IsDeleted = 0) AS NextDue
            FROM Vehicles v
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        foreach (var c in candidates)
        {
            var score = 10m;
            if (c.NextDue is DateTime due && due < DateTime.UtcNow) score += 40;
            else if (c.NextDue is DateTime due2 && due2 < DateTime.UtcNow.AddDays(14)) score += 25;
            if (c.Odometer is > 150000) score += 20;
            else if (c.Odometer is > 100000) score += 10;
            if (c.Battery is < 25) score += 15;
            score = Math.Clamp(score, 0, 99);

            if (score < 40) continue;

            var days = score >= 70 ? 7 : score >= 55 ? 14 : 30;
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO AiPredictions
                    (TenantId, EntityType, EntityId, PredictionType, Probability, ExpectedDays, Label, ModelVersion, CreatedAt)
                VALUES
                    (@TenantId, 'Vehicle', @EntityId, 'maintenance_failure', @Prob, @Days,
                     @Label, 'heuristic-v1', GETUTCDATE())
                """,
                new
                {
                    TenantId = tenantId,
                    EntityId = c.VehicleId,
                    Prob = score,
                    Days = days,
                    Label = score >= 70 ? "High maintenance risk" : "Elevated maintenance risk"
                },
                cancellationToken: cancellationToken));
        }

        // Driver risk: recent overspeeds
        var drivers = await connection.QueryAsync<(int DriverId, int OverspeedCount)>(new CommandDefinition("""
            SELECT TOP 50 a.DriverId, COUNT(*) AS OverspeedCount
            FROM GpsAlertEvents a
            WHERE a.IsDeleted = 0 AND a.EventType = 'speed_exceeded' AND a.DriverId IS NOT NULL
              AND a.Timestamp > DATEADD(DAY, -7, GETUTCDATE())
            GROUP BY a.DriverId
            HAVING COUNT(*) >= 3
            """, cancellationToken: cancellationToken));

        foreach (var d in drivers)
        {
            var prob = Math.Clamp(30 + d.OverspeedCount * 8, 0, 95);
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO AiPredictions
                    (TenantId, EntityType, EntityId, PredictionType, Probability, ExpectedDays, Label, ModelVersion, CreatedAt)
                VALUES
                    (@TenantId, 'Driver', @EntityId, 'driver_risk', @Prob, NULL,
                     @Label, 'heuristic-v1', GETUTCDATE())
                """,
                new
                {
                    TenantId = tenantId,
                    EntityId = d.DriverId,
                    Prob = (decimal)prob,
                    Label = $"Elevated risk — {d.OverspeedCount} overspeeds (7d)"
                },
                cancellationToken: cancellationToken));
        }

        // Fuel anomaly: large drop without matching distance (simple heuristic on consecutive positions not available;
        // flag vehicles with fuel level < 10 as leak/theft watch)
        var fuelWatch = await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT TOP 20 v.Id FROM Vehicles v
            INNER JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId AND vcl.FuelLevel IS NOT NULL AND vcl.FuelLevel < 10
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        foreach (var vehicleId in fuelWatch)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO AiPredictions
                    (TenantId, EntityType, EntityId, PredictionType, Probability, ExpectedDays, Label, ModelVersion, CreatedAt)
                VALUES
                    (@TenantId, 'Vehicle', @EntityId, 'fuel_anomaly', 65, 1,
                     'Low fuel — verify theft/leakage', 'heuristic-v1', GETUTCDATE())
                """,
                new { TenantId = tenantId, EntityId = vehicleId },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<AiPredictionDto>> GetPredictionsAsync(
        int tenantId, string? entityType = null, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT TOP 100 Id, EntityType, EntityId, PredictionType, Probability, ExpectedDays, Label, ModelVersion, CreatedAt
            FROM AiPredictions
            WHERE TenantId = @TenantId AND CreatedAt > DATEADD(DAY, -7, GETUTCDATE())
            """;
        if (!string.IsNullOrWhiteSpace(entityType))
            sql += " AND EntityType = @EntityType";
        sql += " ORDER BY Probability DESC, CreatedAt DESC";

        var rows = await connection.QueryAsync<AiPredictionDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, EntityType = entityType }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AiDatasetStatusDto>> GetDatasetStatusAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var featureCount = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM AiFeatureSnapshots WHERE TenantId = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var featureLast = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            "SELECT MAX(CapturedAt) FROM AiFeatureSnapshots WHERE TenantId = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var predCount = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM AiPredictions WHERE TenantId = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var alertCount = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM GpsAlertEvents WHERE IsDeleted = 0 AND Timestamp > DATEADD(DAY, -90, GETUTCDATE())",
            cancellationToken: cancellationToken));
        var tripCount = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM GpsTrips",
            cancellationToken: cancellationToken));

        static string Freshness(DateTime? at) =>
            at is null ? "empty"
            : at > DateTime.UtcNow.AddHours(-24) ? "fresh"
            : at > DateTime.UtcNow.AddDays(-7) ? "stale"
            : "outdated";

        return
        [
            new AiDatasetStatusDto("AiFeatureSnapshots", featureCount, featureLast, Freshness(featureLast)),
            new AiDatasetStatusDto("AiPredictions", predCount, null, predCount > 0 ? "ready" : "empty"),
            new AiDatasetStatusDto("GpsAlertEvents (90d)", alertCount, null, alertCount > 0 ? "ready" : "empty"),
            new AiDatasetStatusDto("GpsTrips", tripCount, null, tripCount > 0 ? "ready" : "empty")
        ];
    }
}

public sealed class AiManagementService(IDbConnectionFactory dbFactory) : IAiManagementService
{
    public async Task<AiProviderConfigDto> GetConfigAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<AiProviderConfigDto>(new CommandDefinition("""
            SELECT Provider, IsEnabled, CopilotEnabled, DecisionEngineEnabled, DigestEnabled, PredictionsEnabled,
                   MonthlyBudgetUsd, SoftTokenLimit, ApiEndpoint, ModelName
            FROM AiProviderConfig WHERE TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return row ?? new AiProviderConfigDto();
    }

    public async Task<AiProviderConfigDto> UpsertConfigAsync(
        int tenantId, AiProviderConfigDto config, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE AiProviderConfig AS t
            USING (SELECT @TenantId AS TenantId) AS s ON t.TenantId = s.TenantId
            WHEN MATCHED THEN UPDATE SET
                Provider = @Provider, IsEnabled = @IsEnabled, CopilotEnabled = @CopilotEnabled,
                DecisionEngineEnabled = @DecisionEngineEnabled, DigestEnabled = @DigestEnabled,
                PredictionsEnabled = @PredictionsEnabled, MonthlyBudgetUsd = @MonthlyBudgetUsd,
                SoftTokenLimit = @SoftTokenLimit, ApiEndpoint = @ApiEndpoint, ModelName = @ModelName,
                UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT
                (TenantId, Provider, IsEnabled, CopilotEnabled, DecisionEngineEnabled, DigestEnabled,
                 PredictionsEnabled, MonthlyBudgetUsd, SoftTokenLimit, ApiEndpoint, ModelName, UpdatedAt)
            VALUES
                (@TenantId, @Provider, @IsEnabled, @CopilotEnabled, @DecisionEngineEnabled, @DigestEnabled,
                 @PredictionsEnabled, @MonthlyBudgetUsd, @SoftTokenLimit, @ApiEndpoint, @ModelName, GETUTCDATE());
            """,
            new
            {
                TenantId = tenantId,
                config.Provider,
                config.IsEnabled,
                config.CopilotEnabled,
                config.DecisionEngineEnabled,
                config.DigestEnabled,
                config.PredictionsEnabled,
                config.MonthlyBudgetUsd,
                config.SoftTokenLimit,
                config.ApiEndpoint,
                config.ModelName
            },
            cancellationToken: cancellationToken));

        return await GetConfigAsync(tenantId, cancellationToken);
    }

    public async Task RecordUsageAsync(
        int tenantId, string feature, string provider, int tokens, decimal? costUsd = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AiUsageLedger (TenantId, Feature, Provider, TokensUsed, CostUsd, CreatedAt)
            VALUES (@TenantId, @Feature, @Provider, @Tokens, @Cost, GETUTCDATE())
            """,
            new { TenantId = tenantId, Feature = feature, Provider = provider, Tokens = tokens, Cost = costUsd },
            cancellationToken: cancellationToken));
    }

    public async Task RecordLearningAsync(
        int tenantId, int userId, string eventType, string action, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AiLearningSignals (TenantId, UserId, EventType, Action, CreatedAt)
            VALUES (@TenantId, @UserId, @EventType, @Action, GETUTCDATE())
            """,
            new { TenantId = tenantId, UserId = userId, EventType = eventType, Action = action },
            cancellationToken: cancellationToken));

        // Simple learning: repeated Ignore on same event type → bump cooldown for that user via global rule nudge
        var ignores = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM AiLearningSignals
            WHERE TenantId = @TenantId AND UserId = @UserId AND EventType = @EventType AND Action = 'Ignore'
              AND CreatedAt > DATEADD(DAY, -7, GETUTCDATE())
            """, new { TenantId = tenantId, UserId = userId, EventType = eventType }, cancellationToken: cancellationToken));

        if (ignores >= 5)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE AiCooldownRules SET CooldownMinutes = CASE WHEN CooldownMinutes < 120 THEN CooldownMinutes + 15 ELSE CooldownMinutes END,
                    UpdatedAt = GETUTCDATE()
                WHERE EventType = @EventType AND (TenantId = @TenantId OR TenantId IS NULL)
                """, new { EventType = eventType, TenantId = tenantId }, cancellationToken: cancellationToken));
        }
    }
}

public sealed class AiCopilotService(
    IDbConnectionFactory dbFactory,
    IFleetHealthService fleetHealth,
    IAiRecommendationService recommendations,
    IAiPredictionService predictions,
    IAiManagementService aiManagement) : IAiCopilotService
{
    public async Task<AiCopilotResponse> AskAsync(
        int tenantId, int userId, string question, CancellationToken cancellationToken = default)
    {
        var config = await aiManagement.GetConfigAsync(tenantId, cancellationToken);
        var q = question.Trim().ToLowerInvariant();
        var tools = new List<string>();

        // Structured tool routing — LLM only if enabled (otherwise rule-based answers)
        if (q.Contains("offline"))
        {
            tools.Add("offline_vehicles");
            using var connection = dbFactory.CreateConnection();
            var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM VehicleCurrentLocation vcl
                INNER JOIN Vehicles v ON v.Id = vcl.VehicleId AND v.IsDeleted = 0 AND v.TenantId = @TenantId
                WHERE vcl.LastUpdate < DATEADD(MINUTE, -30, GETUTCDATE())
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
            var answer = $"{count} vehicle(s) appear offline (no GPS for 30+ minutes).";
            await MaybeBillAsync(tenantId, config, cancellationToken);
            return new AiCopilotResponse(answer, config.CopilotEnabled && config.IsEnabled ? "hybrid" : "rules", tools, false);
        }

        if (q.Contains("critical") || q.Contains("alert"))
        {
            tools.Add("critical_alerts");
            using var connection = dbFactory.CreateConnection();
            var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM GpsAlertEvents
                WHERE IsDeleted = 0 AND IsAcknowledged = 0 AND Severity = 'critical'
                """, cancellationToken: cancellationToken));
            return new AiCopilotResponse(
                $"There are {count} unacknowledged critical alert(s).",
                "rules", tools, false);
        }

        if (q.Contains("maintenance") || q.Contains("repair"))
        {
            tools.Add("recommendations");
            tools.Add("predictions");
            var recs = await recommendations.GetActiveAsync(tenantId, cancellationToken);
            var preds = await predictions.GetPredictionsAsync(tenantId, "Vehicle", cancellationToken);
            var maintRecs = recs.Where(r => r.Category is "Maintenance" or "Battery").Take(5).ToList();
            var lines = maintRecs.Select(r => $"• {r.Title}: {r.Action}");
            var risk = preds.Where(p => p.PredictionType == "maintenance_failure").Take(3)
                .Select(p => $"• Vehicle #{p.EntityId}: {p.Probability:0}% ({p.Label})");
            var answer = "Maintenance priorities:\n" +
                         (lines.Any() ? string.Join("\n", lines) : "• No open maintenance recommendations.") +
                         "\n\nPredicted risks:\n" +
                         (risk.Any() ? string.Join("\n", risk) : "• No elevated predictions.");
            return new AiCopilotResponse(answer, "rules", tools, false);
        }

        if (q.Contains("health") || q.Contains("fleet"))
        {
            tools.Add("fleet_health");
            var health = await fleetHealth.ComputeAsync(tenantId, cancellationToken);
            return new AiCopilotResponse(health.Summary + $" GPS online {health.GpsOnlineRate:0.#}%, driver score {health.DriverScore:0.#}.",
                "rules", tools, false);
        }

        if (q.Contains("overspeed") || q.Contains("driver"))
        {
            tools.Add("driver_risk");
            var preds = await predictions.GetPredictionsAsync(tenantId, "Driver", cancellationToken);
            var top = preds.Take(5).Select(p => $"• Driver #{p.EntityId}: {p.Probability:0}% — {p.Label}");
            return new AiCopilotResponse(
                top.Any() ? "Driver risk today:\n" + string.Join("\n", top) : "No elevated driver risk predictions.",
                "rules", tools, false);
        }

        tools.Add("help");
        return new AiCopilotResponse(
            "I can answer: offline vehicles, critical alerts, maintenance priorities, fleet health, driver overspeed risk. " +
            (config.CopilotEnabled
                ? "LLM copilot is enabled — connect Azure OpenAI credentials in AI Management for natural-language narratives."
                : "LLM copilot is disabled; answers use SheikhGo rule engines only."),
            "rules", tools, false);
    }

    private Task MaybeBillAsync(int tenantId, AiProviderConfigDto config, CancellationToken ct) =>
        config.IsEnabled
            ? aiManagement.RecordUsageAsync(tenantId, "copilot", config.Provider, 0, null, ct)
            : Task.CompletedTask;
}
