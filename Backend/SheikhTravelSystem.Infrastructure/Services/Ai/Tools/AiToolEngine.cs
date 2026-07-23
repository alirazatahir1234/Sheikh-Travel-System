using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Tools;

public sealed class AiToolEngine(
    IEnumerable<IAiTool> tools,
    AiEntityResolver entityResolver,
    ILogger<AiToolEngine> logger) : IAiToolEngine
{
    private readonly IReadOnlyList<IAiTool> _tools = tools.ToList();

    public IReadOnlyList<AiToolDescriptor> ListTools(bool includeWriteTools = true)
        => _tools
            .Where(t => includeWriteTools || t.Kind == "read")
            .Select(t => new AiToolDescriptor(t.Name, t.Description, t.Kind, t.RequiresConfirmation))
            .ToList();

    public async Task<IReadOnlyList<AiToolExecutionResult>> SelectAndExecuteAsync(
        AiToolExecutionContext context,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var q = userMessage.Trim().ToLowerInvariant();
        var selected = new List<IAiTool>();

        foreach (var tool in _tools)
        {
            if (tool.Kind == "write" && !context.AllowWriteTools)
                continue;

            if (tool.TriggerKeywords.Any(k => q.Contains(k, StringComparison.OrdinalIgnoreCase)))
                selected.Add(tool);
        }

        if (selected.Count == 0 && LooksOperational(q))
        {
            var health = _tools.FirstOrDefault(t => t.Name == "GetFleetHealth");
            if (health is not null) selected.Add(health);
        }

        selected = selected.DistinctBy(t => t.Name).Take(4).ToList();

        var results = new List<AiToolExecutionResult>();
        foreach (var tool in selected)
        {
            try
            {
                var args = await BuildArgsForToolAsync(tool.Name, context, userMessage, cancellationToken);
                results.Add(await tool.ExecuteAsync(context, args, cancellationToken));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AI tool {Tool} failed", tool.Name);
                results.Add(new AiToolExecutionResult(tool.Name, false, string.Empty, Error: ex.Message));
            }
        }

        return results;
    }

    private async Task<JsonElement?> BuildArgsForToolAsync(
        string toolName,
        AiToolExecutionContext context,
        string userMessage,
        CancellationToken cancellationToken)
    {
        if (toolName == "GetVehicleStatus")
        {
            var plate = ExtractPlateHint(userMessage);
            if (plate is null) return null;
            return JsonSerializer.SerializeToElement(new { plate });
        }

        if (toolName == "AssignDriver")
        {
            int? bookingId = ExtractIdAfterKeyword(userMessage, "booking");
            int? driverId = ExtractIdAfterKeyword(userMessage, "driver");

            if (driverId is null)
            {
                var driverName = ExtractDriverNameHint(userMessage);
                if (driverName is not null)
                    driverId = await entityResolver.ResolveDriverIdAsync(context.TenantId, driverName, cancellationToken);
            }

            if (bookingId is null || driverId is null)
            {
                var ids = ExtractTwoInts(userMessage);
                if (ids is not null)
                {
                    bookingId ??= ids.Value.A;
                    driverId ??= ids.Value.B;
                }
            }

            if (bookingId is null || driverId is null) return null;
            return JsonSerializer.SerializeToElement(new { bookingId, driverId });
        }

        if (toolName == "SendNotification")
        {
            return JsonSerializer.SerializeToElement(new
            {
                title = "SheikhGo AI reminder",
                message = userMessage.Length > 200 ? userMessage[..200] : userMessage
            });
        }

        return null;
    }

    private static int? ExtractIdAfterKeyword(string message, string keyword)
    {
        var m = Regex.Match(message, $@"{keyword}\s+#?(\d+)", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var id) ? id : null;
    }

    private static string? ExtractDriverNameHint(string message)
    {
        var m = Regex.Match(message, @"assign(?:\s+the)?\s+driver\s+([A-Za-z][A-Za-z\s'-]{1,40}?)(?:\s+to|\s*$)", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value.Trim();
        return null;
    }

    private static string? ExtractPlateHint(string message)
    {
        var quoted = Regex.Match(message, "[\"']([A-Za-z0-9\\-]{4,})[\"']");
        if (quoted.Success) return quoted.Groups[1].Value;

        var tokens = Regex.Matches(message, @"\b[A-Za-z]{1,3}[-\s]?\d{2,4}[-\s]?[A-Za-z0-9]{0,4}\b");
        return tokens.Count > 0 ? tokens[^1].Value.Replace(" ", "") : null;
    }

    private static (int A, int B)? ExtractTwoInts(string message)
    {
        var nums = System.Text.RegularExpressions.Regex.Matches(message, @"\b\d+\b")
            .Select(m => int.Parse(m.Value))
            .ToList();
        return nums.Count >= 2 ? (nums[0], nums[1]) : null;
    }

    public async Task<AiToolExecutionResult> ExecuteAsync(
        string toolName,
        AiToolExecutionContext context,
        JsonElement? args = null,
        CancellationToken cancellationToken = default)
    {
        var tool = _tools.FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
        if (tool is null)
            return new AiToolExecutionResult(toolName, false, string.Empty, Error: $"Unknown tool '{toolName}'.");

        if (tool.Kind == "write" && !context.AllowWriteTools)
            return new AiToolExecutionResult(toolName, false, string.Empty, Error: "Write tools are disabled for this session.");

        if (tool.RequiresConfirmation && !context.ConfirmWrite)
        {
            var preview = await tool.ExecuteAsync(
                context with { ConfirmWrite = false },
                args,
                cancellationToken);
            return preview with { PendingConfirmation = true };
        }

        return await tool.ExecuteAsync(context, args, cancellationToken);
    }

    private static bool LooksOperational(string q)
        => q.Contains("fleet") || q.Contains("vehicle") || q.Contains("gps")
           || q.Contains("driver") || q.Contains("status") || q.Contains("how")
           || q.Contains("what") || q.Contains("show") || q.Contains("list");
}

public sealed class GetFleetHealthTool(IFleetHealthService fleetHealth) : IAiTool
{
    public string Name => "GetFleetHealth";
    public string Description => "Returns overall fleet health %, GPS online rate, maintenance/compliance scores, and critical alert count.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } = ["health", "fleet", "overview", "summary", "dashboard"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var h = await fleetHealth.ComputeAsync(context.TenantId, cancellationToken);
        var summary =
            $"Fleet health {h.HealthPercent:0.#}%. GPS online {h.GpsOnlineRate:0.#}%. " +
            $"Maintenance {h.MaintenanceScore:0.#}%, compliance {h.ComplianceScore:0.#}%, " +
            $"driver score {h.DriverScore:0.#}. Critical alerts: {h.CriticalAlerts}. {h.Summary}";
        return new AiToolExecutionResult(Name, true, summary, h);
    }
}

public sealed class GetOfflineVehiclesTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetOfflineVehicles";
    public string Description => "Lists vehicles with no GPS update for 30+ minutes.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } = ["offline", "no gps", "not reporting", "disconnected"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<(int Id, string PlateNumber, string? Make, string? Model, DateTime? LastUpdate)>(
            new CommandDefinition("""
                SELECT TOP 25 v.Id, v.PlateNumber, v.Make, v.Model, vcl.LastUpdate
                FROM Vehicles v
                LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId
                  AND (vcl.LastUpdate IS NULL OR vcl.LastUpdate < DATEADD(MINUTE, -30, GETUTCDATE()))
                ORDER BY vcl.LastUpdate ASC
                """, new { context.TenantId }, cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
            return new AiToolExecutionResult(Name, true, "No vehicles appear offline (all reported GPS within 30 minutes).", rows);

        var sb = new StringBuilder();
        sb.AppendLine($"{rows.Count} vehicle(s) offline / not reporting:");
        foreach (var r in rows.Take(15))
        {
            var ago = r.LastUpdate is null ? "never" : r.LastUpdate.Value.ToString("u");
            sb.AppendLine($"• #{r.Id} {r.PlateNumber} ({r.Make} {r.Model}) — last GPS: {ago}");
        }

        return new AiToolExecutionResult(Name, true, sb.ToString().Trim(), rows);
    }
}

public sealed class GetCriticalAlertsTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetCriticalAlerts";
    public string Description => "Lists unacknowledged critical GPS alerts from the last 24 hours.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } = ["critical", "alert", "alarm", "sos"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<(int Id, string EventType, string Severity, DateTime Timestamp, int? VehicleId)>(
            new CommandDefinition("""
                SELECT TOP 20 Id, EventType, Severity, Timestamp, VehicleId
                FROM GpsAlertEvents
                WHERE IsDeleted = 0 AND IsAcknowledged = 0 AND Severity = 'critical'
                  AND Timestamp > DATEADD(DAY, -1, GETUTCDATE())
                ORDER BY Timestamp DESC
                """, cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
            return new AiToolExecutionResult(Name, true, "No unacknowledged critical alerts in the last 24 hours.", rows);

        var sb = new StringBuilder();
        sb.AppendLine($"{rows.Count} critical alert(s):");
        foreach (var r in rows)
            sb.AppendLine($"• #{r.Id} {r.EventType} — vehicle {(r.VehicleId?.ToString() ?? "n/a")} @ {r.Timestamp:u}");

        return new AiToolExecutionResult(Name, true, sb.ToString().Trim(), rows);
    }
}

public sealed class GetMaintenancePrioritiesTool(
    IAiRecommendationService recommendations,
    IAiPredictionService predictions) : IAiTool
{
    public string Name => "GetMaintenancePriorities";
    public string Description => "Returns open maintenance recommendations and failure-risk predictions.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } = ["maintenance", "repair", "service", "overdue", "workshop"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var recs = await recommendations.GetActiveAsync(context.TenantId, cancellationToken);
        var preds = await predictions.GetPredictionsAsync(context.TenantId, "Vehicle", cancellationToken);
        var maintRecs = recs.Where(r => r.Category is "Maintenance" or "Battery").Take(8).ToList();
        var risk = preds.Where(p => p.PredictionType == "maintenance_failure").Take(5).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Maintenance priorities:");
        if (maintRecs.Count == 0) sb.AppendLine("• No open maintenance recommendations.");
        else foreach (var r in maintRecs) sb.AppendLine($"• {r.Title}: {r.Action}");

        sb.AppendLine();
        sb.AppendLine("Predicted risks:");
        if (risk.Count == 0) sb.AppendLine("• No elevated predictions.");
        else foreach (var p in risk) sb.AppendLine($"• Vehicle #{p.EntityId}: {p.Probability:0}% ({p.Label})");

        return new AiToolExecutionResult(Name, true, sb.ToString().Trim(), new { maintRecs, risk });
    }
}

public sealed class GetDriverRiskTool(IAiPredictionService predictions) : IAiTool
{
    public string Name => "GetDriverRisk";
    public string Description => "Returns elevated driver risk predictions (overspeed / safety).";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } = ["driver", "overspeed", "risk", "safety", "behaviour", "behavior"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var preds = await predictions.GetPredictionsAsync(context.TenantId, "Driver", cancellationToken);
        var top = preds.Take(8).ToList();
        if (top.Count == 0)
            return new AiToolExecutionResult(Name, true, "No elevated driver risk predictions.", top);

        var sb = new StringBuilder("Driver risk today:");
        sb.AppendLine();
        foreach (var p in top)
            sb.AppendLine($"• Driver #{p.EntityId}: {p.Probability:0}% — {p.Label}");

        return new AiToolExecutionResult(Name, true, sb.ToString().Trim(), top);
    }
}

public sealed class GetVehicleStatusTool(IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetVehicleStatus";
    public string Description => "Looks up a vehicle by plate number fragment and returns GPS/status snapshot.";
    public string Kind => "read";
    public bool RequiresConfirmation => false;
    public IReadOnlyList<string> TriggerKeywords { get; } = ["plate", "registration", "vehicle #", "vehicle status", "where is"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        string? plate = null;
        if (args is { } el && el.ValueKind == JsonValueKind.Object && el.TryGetProperty("plate", out var p))
            plate = p.GetString();

        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<(int Id, string PlateNumber, string Status, decimal? Speed, DateTime? LastUpdate, decimal? Lat, decimal? Lng)>(
            new CommandDefinition("""
                SELECT TOP 10 v.Id, v.PlateNumber, ISNULL(v.Status, 'Unknown') AS Status,
                       vcl.Speed, vcl.LastUpdate, vcl.Latitude AS Lat, vcl.Longitude AS Lng
                FROM Vehicles v
                LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId
                  AND (@Plate IS NULL OR v.PlateNumber LIKE '%' + @Plate + '%')
                ORDER BY vcl.LastUpdate DESC
                """, new { context.TenantId, Plate = plate }, cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
            return new AiToolExecutionResult(Name, true, "No matching vehicles found.", rows);

        var sb = new StringBuilder();
        foreach (var r in rows)
        {
            sb.AppendLine(
                $"• #{r.Id} {r.PlateNumber} — status {r.Status}, speed {(r.Speed?.ToString("0.#") ?? "—")} km/h, " +
                $"last GPS {(r.LastUpdate?.ToString("u") ?? "never")}, pos {(r.Lat is null ? "—" : $"{r.Lat:0.####},{r.Lng:0.####}")}");
        }

        return new AiToolExecutionResult(Name, true, sb.ToString().Trim(), rows);
    }
}

/// <summary>Phase 3 write tool — assigns driver to booking via MediatR (validation + audit).</summary>
public sealed class AssignDriverTool(IMediator mediator, AiEntityResolver entityResolver) : IAiTool
{
    public string Name => "AssignDriver";
    public string Description => "Assigns a driver to a booking. Requires confirmation before write.";
    public string Kind => "write";
    public bool RequiresConfirmation => true;
    public IReadOnlyList<string> TriggerKeywords { get; } =
        ["assign driver", "assign the driver", "set driver"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        int? bookingId = null;
        int? driverId = null;
        if (args is { } el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("bookingId", out var b) && b.TryGetInt32(out var bi)) bookingId = bi;
            if (el.TryGetProperty("driverId", out var d) && d.TryGetInt32(out var di)) driverId = di;
        }

        if (bookingId is null || driverId is null)
        {
            return new AiToolExecutionResult(
                Name, false, string.Empty,
                Error: "AssignDriver needs bookingId and driverId. Example: assign driver Ahmed to booking 45.");
        }

        var labels = await entityResolver.GetAssignmentLabelsAsync(
            context.TenantId, bookingId.Value, driverId.Value, cancellationToken);
        var draftSummary =
            $"Proposed: assign **{labels.DriverName ?? $"driver #{driverId}"}** to booking **{labels.BookingRef}**.";

        if (!context.ConfirmWrite)
        {
            return new AiToolExecutionResult(
                Name,
                true,
                draftSummary + " Click **Confirm** or reply **CONFIRM** to apply.",
                new { bookingId, driverId },
                PendingConfirmation: true);
        }

        var response = await mediator.Send(
            new AssignDriverCommand(bookingId.Value, driverId.Value),
            cancellationToken);

        if (!response.Success)
        {
            return new AiToolExecutionResult(
                Name, false, string.Empty,
                Error: response.Message);
        }

        return new AiToolExecutionResult(
            Name, true,
            $"Assigned {labels.DriverName ?? $"driver #{driverId}"} to booking {labels.BookingRef}.",
            new { bookingId, driverId });
    }
}

/// <summary>Phase 2 write tool — drafts a notification payload; sends only on confirm.</summary>
public sealed class SendNotificationTool(INotificationService notifications) : IAiTool
{
    public string Name => "SendNotification";
    public string Description => "Sends an in-app notification to the current user (or drafts one pending confirmation).";
    public string Kind => "write";
    public bool RequiresConfirmation => true;
    public IReadOnlyList<string> TriggerKeywords { get; } = ["notify me", "send notification", "remind me"];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var title = "SheikhGo AI reminder";
        var message = "You asked SheikhGo AI to notify you.";
        if (args is { } el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("title", out var t)) title = t.GetString() ?? title;
            if (el.TryGetProperty("message", out var m)) message = m.GetString() ?? message;
        }

        if (!context.ConfirmWrite)
        {
            return new AiToolExecutionResult(
                Name, true,
                $"Proposed notification: “{title}” — {message}. Click **Confirm** or reply **CONFIRM** to send.",
                new { title, message },
                PendingConfirmation: true);
        }

        await notifications.CreateAsync(
            context.UserId,
            title,
            message,
            NotificationType.TripUpdated,
            cancellationToken: cancellationToken);

        return new AiToolExecutionResult(Name, true, $"Notification sent: {title}");
    }
}
