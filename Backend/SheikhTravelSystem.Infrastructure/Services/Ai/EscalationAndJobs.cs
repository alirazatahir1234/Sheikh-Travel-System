using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Ai;

public sealed class EscalationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<EscalationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await ProcessAsync(scope.ServiceProvider, logger, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Escalation cycle failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private static async Task ProcessAsync(IServiceProvider sp, ILogger<EscalationHostedService> logger, CancellationToken ct)
    {
        var db = sp.GetRequiredService<IDbConnectionFactory>();
        var notifications = sp.GetRequiredService<INotificationService>();
        using var connection = db.CreateConnection();

        var due = (await connection.QueryAsync<EscalationDueRow>(
            new CommandDefinition("""
                SELECT TOP 20 Id, TenantId, EventType, CurrentLevel, ReferenceId
                FROM EscalationState
                WHERE Status = 'Pending' AND NextEscalateAt IS NOT NULL AND NextEscalateAt <= GETUTCDATE()
                ORDER BY NextEscalateAt
                """, cancellationToken: ct))).ToList();

        foreach (var item in due)
        {
            if (item.TenantId is not int tenantId || tenantId <= 0)
            {
                logger.LogWarning("Escalation {Id} has no TenantId; skipping", item.Id);
                continue;
            }

            try
            {
                var nextLevel = item.CurrentLevel + 1;
                var rule = await connection.QuerySingleOrDefaultAsync<EscalationRuleRow>(
                    new CommandDefinition("""
                        SELECT TOP 1 TargetRole, TimeoutMinutes, Channel FROM EscalationRules
                        WHERE IsActive = 1 AND EventType = @EventType AND LevelOrder = @Level
                          AND (TenantId = @TenantId OR TenantId IS NULL)
                        ORDER BY CASE WHEN TenantId IS NULL THEN 1 ELSE 0 END
                        """,
                        new { item.EventType, Level = nextLevel, item.TenantId },
                        cancellationToken: ct));

                if (rule is null)
                {
                    await connection.ExecuteAsync(new CommandDefinition("""
                        UPDATE EscalationState SET Status = 'Exhausted', UpdatedAt = GETUTCDATE() WHERE Id = @Id
                        """, new { item.Id }, cancellationToken: ct));
                    continue;
                }

                await notifications.CreateForAllChannelsAsync(
                    $"Escalation L{nextLevel}: {item.EventType}",
                    $"No acknowledgement for {item.EventType} (ref #{item.ReferenceId}). Notifying {rule.TargetRole}.",
                    NotificationType.Sos,
                    [rule.Channel, NotificationChannels.InApp],
                    priority: 4,
                    module: "Fleet",
                    referenceId: item.ReferenceId,
                    tenantId: tenantId,
                    cancellationToken: ct);

                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE EscalationState SET
                        CurrentLevel = @Level,
                        NextEscalateAt = DATEADD(MINUTE, @Timeout, GETUTCDATE()),
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id
                    """,
                    new { item.Id, Level = nextLevel, Timeout = rule.TimeoutMinutes },
                    cancellationToken: ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Escalation item {Id} failed", item.Id);
            }
        }
    }

    private sealed class EscalationDueRow
    {
        public int Id { get; init; }
        public int? TenantId { get; init; }
        public string EventType { get; init; } = "";
        public int CurrentLevel { get; init; }
        public int? ReferenceId { get; init; }
    }

    private sealed class EscalationRuleRow
    {
        public string TargetRole { get; init; } = "";
        public int TimeoutMinutes { get; init; }
        public string Channel { get; init; } = "InApp";
    }
}

public sealed class EscalationService(IDbConnectionFactory dbFactory) : IEscalationService
{
    public Task StartAsync(
        string eventType,
        int? referenceId,
        int? alertEventId = null,
        int? notificationId = null,
        CancellationToken cancellationToken = default) =>
        EscalationStarter.StartAsync(dbFactory, eventType, referenceId, alertEventId, notificationId, cancellationToken);

    public async Task<IReadOnlyList<EscalationRuleDto>> GetRulesAsync(
        int? tenantId = null, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<EscalationRuleDto>(new CommandDefinition("""
            SELECT Id, TenantId, EventType, LevelOrder, TargetRole, TimeoutMinutes, Channel, IsActive
            FROM EscalationRules
            WHERE (@TenantId IS NULL OR TenantId = @TenantId OR TenantId IS NULL)
            ORDER BY EventType, LevelOrder
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<EscalationRuleDto> UpsertRuleAsync(EscalationRuleDto rule, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (rule.Id > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE EscalationRules SET
                    EventType = @EventType, LevelOrder = @LevelOrder, TargetRole = @TargetRole,
                    TimeoutMinutes = @TimeoutMinutes, Channel = @Channel, IsActive = @IsActive
                WHERE Id = @Id
                """, rule, cancellationToken: cancellationToken));
            return rule;
        }

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO EscalationRules
                (TenantId, EventType, LevelOrder, TargetRole, TimeoutMinutes, Channel, IsActive, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@TenantId, @EventType, @LevelOrder, @TargetRole, @TimeoutMinutes, @Channel, @IsActive, GETUTCDATE())
            """, rule, cancellationToken: cancellationToken));
        return rule with { Id = id };
    }

    public async Task<IReadOnlyList<EscalationPendingDto>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<EscalationPendingDto>(new CommandDefinition("""
            SELECT TOP 100 Id, EventType, CurrentLevel, ReferenceId, AlertEventId, NextEscalateAt, Status, CreatedAt
            FROM EscalationState
            WHERE Status = 'Pending'
            ORDER BY NextEscalateAt
            """, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task AcknowledgeAsync(int stateId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE EscalationState SET Status = 'Acknowledged', UpdatedAt = GETUTCDATE() WHERE Id = @Id
            """, new { Id = stateId }, cancellationToken: cancellationToken));
    }
}

public static class EscalationStarter
{
    public static async Task StartAsync(
        IDbConnectionFactory dbFactory,
        string eventType,
        int? referenceId,
        int? alertEventId,
        int? notificationId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var firstTimeout = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 TimeoutMinutes FROM EscalationRules
            WHERE IsActive = 1 AND EventType = @EventType AND LevelOrder = 1
            """, new { EventType = eventType }, cancellationToken: cancellationToken)) ?? 15;

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM EscalationState
                WHERE Status = 'Pending' AND EventType = @EventType
                  AND ISNULL(ReferenceId,0) = ISNULL(@ReferenceId,0))
            INSERT INTO EscalationState
                (NotificationId, AlertEventId, EventType, ReferenceId, CurrentLevel, Status, NextEscalateAt, CreatedAt)
            VALUES
                (@NotificationId, @AlertEventId, @EventType, @ReferenceId, 0, 'Pending',
                 DATEADD(MINUTE, @Timeout, GETUTCDATE()), GETUTCDATE())
            """,
            new
            {
                NotificationId = notificationId,
                AlertEventId = alertEventId,
                EventType = eventType,
                ReferenceId = referenceId,
                Timeout = firstTimeout
            },
            cancellationToken: cancellationToken));
    }
}

public sealed class AiJobsHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AiJobsHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                if (tenantContext.TenantId is not int tenantId || tenantId <= 0)
                {
                    logger.LogDebug("Skipping AI jobs cycle because tenant context is unavailable.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                var health = scope.ServiceProvider.GetRequiredService<IFleetHealthService>();
                var digest = scope.ServiceProvider.GetRequiredService<IAiDigestService>();
                var recs = scope.ServiceProvider.GetRequiredService<IAiRecommendationService>();
                var preds = scope.ServiceProvider.GetRequiredService<IAiPredictionService>();

                await health.ComputeAsync(tenantId, stoppingToken);
                await recs.RefreshAsync(tenantId, stoppingToken);
                await preds.CaptureFeaturesAsync(tenantId, stoppingToken);
                await preds.RunHeuristicPredictionsAsync(tenantId, stoppingToken);

                if (DateTime.UtcNow.Hour == 4)
                    await digest.GenerateMorningDigestAsync(tenantId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "AI jobs cycle failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
