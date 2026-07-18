using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;

namespace SheikhTravelSystem.Infrastructure.Services.Notifications;

public sealed class NotificationRetentionService(
    IDbConnectionFactory dbFactory,
    ILogger<NotificationRetentionService> logger) : INotificationRetentionService
{
    public async Task<NotificationRetentionEstimateDto> RunCleanupAsync(
        int? tenantId = null, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tid = tenantId ?? 1;

        var rows = (await connection.QueryAsync<(string Key, string? Value)>(new CommandDefinition("""
            SELECT [Key], Value FROM PlatformSettings
            WHERE TenantId = @TenantId AND Category = @Category AND IsActive = 1
            """,
            new { TenantId = tid, Category = NotificationRetention.SettingsCategory },
            cancellationToken: cancellationToken))).ToList();

        var policy = NotificationRetentionPolicy.FromDictionary(
            rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));

        var archiveCutoff = DateTime.UtcNow.AddDays(-Math.Max(1, policy.ReadArchiveDays));

        var archived = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE r SET
                r.IsArchived = 1,
                r.ArchivedAt = GETUTCDATE()
            FROM NotificationRecipients r
            INNER JOIN Notifications n ON n.Id = r.NotificationId
            WHERE r.IsDeleted = 0 AND r.IsArchived = 0 AND r.IsRead = 1
              AND ISNULL(n.NeverAutoDelete, 0) = 0
              AND ISNULL(n.RetentionCategory, 'Standard') <> 'Critical'
              AND ISNULL(r.ReadAt, n.ReadDate) IS NOT NULL
              AND ISNULL(r.ReadAt, n.ReadDate) < @Cutoff
            """,
            new { Cutoff = archiveCutoff },
            cancellationToken: cancellationToken));

        // Hard-delete recipient rows past retention (skip NeverAutoDelete / Critical when configured)
        var deletedRecipients = 0;
        foreach (var category in new[]
                 {
                     NotificationRetention.Standard,
                     NotificationRetention.Operational,
                     NotificationRetention.Maintenance,
                     NotificationRetention.Compliance,
                     NotificationRetention.Security,
                     NotificationRetention.Ai
                 })
        {
            var days = policy.DeleteDaysForCategory(category);
            if (days >= int.MaxValue / 2) continue;

            deletedRecipients += await connection.ExecuteAsync(new CommandDefinition("""
                DELETE r
                FROM NotificationRecipients r
                INNER JOIN Notifications n ON n.Id = r.NotificationId
                WHERE ISNULL(n.NeverAutoDelete, 0) = 0
                  AND ISNULL(n.RetentionCategory, 'Standard') = @Category
                  AND (
                        (r.IsDeleted = 1 AND r.DeletedAt IS NOT NULL AND r.DeletedAt < DATEADD(DAY, -@Days, GETUTCDATE()))
                     OR (r.IsArchived = 1 AND r.ArchivedAt IS NOT NULL AND r.ArchivedAt < DATEADD(DAY, -@Days, GETUTCDATE()))
                  )
                """,
                new { Category = category, Days = days },
                cancellationToken: cancellationToken));
        }

        // Failed deliveries past FailedDeleteDays (non-critical)
        var failedDeleted = await connection.ExecuteAsync(new CommandDefinition("""
            DELETE r
            FROM NotificationRecipients r
            INNER JOIN Notifications n ON n.Id = r.NotificationId
            WHERE ISNULL(n.NeverAutoDelete, 0) = 0
              AND ISNULL(n.DeliveryStatus, '') = 'Failed'
              AND ISNULL(n.RetentionCategory, 'Standard') <> 'Critical'
              AND n.CreatedAt < DATEADD(DAY, -@Days, GETUTCDATE())
            """,
            new { Days = Math.Max(1, policy.FailedDeleteDays) },
            cancellationToken: cancellationToken));

        // Orphan notifications with no recipients left (and not NeverAutoDelete)
        var orphanDeleted = await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM NotificationDeliveryLogs
            WHERE NotificationId IN (
                SELECT n.Id FROM Notifications n
                WHERE ISNULL(n.NeverAutoDelete, 0) = 0
                  AND NOT EXISTS (SELECT 1 FROM NotificationRecipients r WHERE r.NotificationId = n.Id)
                  AND (
                        n.IsDeleted = 1
                     OR ISNULL(n.IsArchived, 0) = 1
                     OR ISNULL(n.DeliveryStatus, '') = 'Failed'
                  )
                  AND n.CreatedAt < DATEADD(DAY, -@Days, GETUTCDATE())
            );

            DELETE FROM Notifications
            WHERE ISNULL(NeverAutoDelete, 0) = 0
              AND NOT EXISTS (SELECT 1 FROM NotificationRecipients r WHERE r.NotificationId = Notifications.Id)
              AND (
                    IsDeleted = 1
                 OR ISNULL(IsArchived, 0) = 1
                 OR ISNULL(DeliveryStatus, '') = 'Failed'
              )
              AND CreatedAt < DATEADD(DAY, -@Days, GETUTCDATE());
            """,
            new { Days = Math.Max(1, policy.ArchivedDeleteDays) },
            cancellationToken: cancellationToken));

        var protectedCritical = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM Notifications
            WHERE ISNULL(NeverAutoDelete, 0) = 1 OR RetentionCategory = 'Critical'
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "Notification retention: archived {Archived}, removed recipients {Recipients}+{Failed}, orphans {Orphans}",
            archived, deletedRecipients, failedDeleted, orphanDeleted);

        return new NotificationRetentionEstimateDto(
            archived,
            deletedRecipients + failedDeleted + orphanDeleted,
            protectedCritical);
    }
}
