using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Services.Notifications;

namespace SheikhTravelSystem.Infrastructure.Services.Ai;

public sealed class NotificationDecisionEngine(
    IDbConnectionFactory dbFactory,
    IDistributedCache cache,
    INotificationService notifications,
    NotificationRecipientResolver recipientResolver,
    IUserPresenceService presence,
    IAlertNotificationAudit alertAudit,
    IAiManagementService aiManagement,
    ITenantContext tenantContext,
    ILogger<NotificationDecisionEngine> logger) : INotificationDecisionEngine
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<NotificationDecisionResult> EvaluateAsync(
        NotificationDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = request.TenantId ?? tenantContext.TenantId ?? 1;
        var config = await aiManagement.GetConfigAsync(tenantId, cancellationToken);
        if (!config.DecisionEngineEnabled)
        {
            return new NotificationDecisionResult(
                true, "Bypass", "Decision engine disabled",
                request.SuggestedPriority ?? 2,
                request.RequestedChannels ?? [NotificationChannels.InApp]);
        }

        using var connection = dbFactory.CreateConnection();
        var rule = await connection.QuerySingleOrDefaultAsync<CooldownRuleRow>(
            new CommandDefinition("""
                SELECT TOP 1 CooldownMinutes, MinPriority, ChannelsJson, CorrelateWith
                FROM AiCooldownRules
                WHERE IsActive = 1 AND EventType = @EventType
                  AND (TenantId = @TenantId OR TenantId IS NULL)
                ORDER BY CASE WHEN TenantId IS NULL THEN 1 ELSE 0 END
                """,
                new { request.EventType, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var cooldownMinutes = rule?.CooldownMinutes ?? DefaultCooldown(request.EventType);
        var minPriority = rule?.MinPriority ?? 1;
        var priority = Math.Max(request.SuggestedPriority ?? InferPriority(request.EventType), minPriority);

        var channels = ParseChannels(rule?.ChannelsJson, request.RequestedChannels, priority);
        var cooldownKey = $"ai:cd:{tenantId}:{request.EventType}:{request.ReferenceId ?? 0}";

        if (cooldownMinutes > 0)
        {
            try
            {
                var existing = await cache.GetStringAsync(cooldownKey, cancellationToken);
                if (!string.IsNullOrEmpty(existing))
                {
                    await WriteAuditAsync(tenantId, request, "Suppress", "Cooldown active", priority, [], cooldownKey, cancellationToken);
                    return new NotificationDecisionResult(false, "Suppress", "Cooldown active", priority, [], cooldownKey);
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Redis optional — skip cooldown when cache unavailable
            }
        }

        // Correlation: if a related open critical already notified recently, suppress medium siblings
        if (!string.IsNullOrWhiteSpace(rule?.CorrelateWith) && request.ReferenceId is int refId)
        {
            var related = rule!.CorrelateWith!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var rel in related)
            {
                var relKey = $"ai:cd:{tenantId}:{rel}:{refId}";
                try
                {
                    if (!string.IsNullOrEmpty(await cache.GetStringAsync(relKey, cancellationToken)))
                    {
                        await WriteAuditAsync(tenantId, request, "Correlate", $"Correlated with {rel}", priority, [], cooldownKey, cancellationToken);
                        return new NotificationDecisionResult(false, "Correlate", $"Suppressed — correlated with {rel}", priority, [], cooldownKey);
                    }
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Redis optional
                }
            }
        }

        if (request.TargetUserId is int uid)
            channels = (await presence.SelectChannelsAsync(uid, priority, channels, cancellationToken)).ToList();

        if (channels.Count == 0)
        {
            await WriteAuditAsync(tenantId, request, "Skip", "No channels after policy", priority, [], cooldownKey, cancellationToken);
            return new NotificationDecisionResult(false, "Skip", "No channels after preference/presence policy", priority, [], cooldownKey);
        }

        if (cooldownMinutes > 0)
        {
            try
            {
                await cache.SetStringAsync(
                    cooldownKey, "1",
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cooldownMinutes) },
                    cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Redis optional
            }
        }

        await WriteAuditAsync(tenantId, request, "Notify", "Allowed", priority, channels, cooldownKey, cancellationToken);
        return new NotificationDecisionResult(true, "Notify", "Allowed", priority, channels, cooldownKey);
    }

    public async Task<int> DispatchIfAllowedAsync(
        NotificationDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = request.TenantId ?? tenantContext.GetRequiredTenantId();
        var decision = await EvaluateAsync(request, cancellationToken);
        if (!decision.ShouldNotify)
        {
            logger.LogDebug("Notification suppressed ({Decision}): {EventType} — {Reason}",
                decision.Decision, request.EventType, decision.Reason);
            return 0;
        }

        if (request.Broadcast)
        {
            if (!NotificationRecipientPolicy.AllowsBroadcast(request.EventType))
            {
                logger.LogWarning("Rejected broadcast for non-system event {EventType}", request.EventType);
                return 0;
            }

            await notifications.CreateForAllChannelsAsync(
                request.Title, request.Message, request.Type, decision.Channels,
                decision.Priority, ModuleFor(request.EventType), request.ReferenceId,
                templateKey: TemplateFor(request.EventType),
                cancellationToken: cancellationToken);

            if (request.AlertEventId is int alertId)
            {
                foreach (var ch in decision.Channels)
                    await alertAudit.LogAsync(alertId, ch, null, "Sent", null, cancellationToken);
            }

            return decision.Channels.Count;
        }

        var recipientIds = await recipientResolver.ResolveUserIdsAsync(request, tenantId, cancellationToken);
        if (recipientIds.Count == 0)
            return 0;

        var count = 0;
        foreach (var userId in recipientIds)
        {
            foreach (var channel in decision.Channels)
            {
                if (!await alertAudit.IsAlertTypeEnabledAsync(userId, request.EventType, channel, cancellationToken))
                    continue;

                await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
                    userId, tenantId, request.Title, request.Message, request.Type, request.ReferenceId,
                    decision.Priority, channel, Module: ModuleFor(request.EventType),
                    TemplateKey: TemplateFor(request.EventType)), cancellationToken);

                if (request.AlertEventId is int alertId)
                    await alertAudit.LogAsync(alertId, channel, userId.ToString(), "Sent", null, cancellationToken);

                count++;
            }
        }
        return count;
    }

    private async Task WriteAuditAsync(
        int tenantId, NotificationDecisionRequest request, string decision, string reason,
        int priority, IReadOnlyList<string> channels, string? cooldownKey, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AiDecisionAudit
                (TenantId, EventType, ReferenceType, ReferenceId, Decision, Reason, Priority, ChannelsJson, CooldownKey, CreatedAt)
            VALUES
                (@TenantId, @EventType, @ReferenceType, @ReferenceId, @Decision, @Reason, @Priority, @ChannelsJson, @CooldownKey, GETUTCDATE())
            """,
            new
            {
                TenantId = tenantId,
                request.EventType,
                ReferenceType = request.AlertEventId is not null ? "GpsAlertEvent" : "Entity",
                request.ReferenceId,
                Decision = decision,
                Reason = reason,
                Priority = priority,
                ChannelsJson = JsonSerializer.Serialize(channels, JsonOpts),
                CooldownKey = cooldownKey
            },
            cancellationToken: ct));
    }

    private static int DefaultCooldown(string eventType) => eventType.ToLowerInvariant() switch
    {
        "sos" => 1,
        "vehicle_offline" => 30,
        "speed_exceeded" => 15,
        "low_fuel" or "low_battery" => 60,
        "compliance_reminder" => 1440,
        _ => 5
    };

    private static int InferPriority(string eventType) => eventType.ToLowerInvariant() switch
    {
        "sos" => 4,
        "vehicle_offline" or "speed_exceeded" or "compliance_reminder" => 3,
        _ => 2
    };

    private static string ModuleFor(string eventType) => eventType.ToLowerInvariant() switch
    {
        "booking_created" => "Booking",
        "trip_driver_assigned" or "trip_started" or "trip_completed"
            or "trip_delayed" or "trip_cancelled" or "trip_updated" or "trip_driver_arriving" => "Trip",
        "payment_received" => "Finance",
        "compliance_reminder" => "Compliance",
        _ => "Fleet"
    };

    private static string? TemplateFor(string eventType) => eventType.ToLowerInvariant() switch
    {
        "sos" => "sos_alert",
        "speed_exceeded" => "speed_alert",
        "vehicle_offline" => "vehicle_offline",
        "booking_created" => "booking_created",
        "trip_driver_assigned" => "trip_driver_assigned",
        "trip_started" => "trip_started",
        "trip_completed" => "trip_completed",
        "trip_delayed" => "trip_delayed",
        "trip_cancelled" => "trip_cancelled",
        "trip_updated" => "trip_updated",
        "trip_driver_arriving" => "trip_driver_arriving",
        "payment_received" => "payment_received",
        "compliance_reminder" => "compliance_reminder",
        _ => null
    };

    private static List<string> ParseChannels(string? json, IReadOnlyList<string>? requested, int priority)
    {
        List<string> channels;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { channels = JsonSerializer.Deserialize<List<string>>(json!) ?? []; }
            catch { channels = []; }
        }
        else
        {
            channels = requested?.ToList() ?? [NotificationChannels.InApp, NotificationChannels.Browser];
        }

        if (requested is { Count: > 0 })
            channels = channels.Intersect(requested, StringComparer.OrdinalIgnoreCase).ToList();

        if (priority < 3)
            channels = channels.Where(c => c is not NotificationChannels.Sms).ToList();

        if (channels.Count == 0)
            channels = [NotificationChannels.InApp];

        return channels.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private sealed class CooldownRuleRow
    {
        public int CooldownMinutes { get; init; }
        public int MinPriority { get; init; }
        public string? ChannelsJson { get; init; }
        public string? CorrelateWith { get; init; }
    }
}
