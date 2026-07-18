using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Notifications.Commands;

public record MarkNotificationsReadCommand(int UserId, List<int>? NotificationIds = null)
    : IRequest<ApiResponse<bool>>;

public class MarkNotificationsReadCommandHandler(
    IDbConnectionFactory dbFactory,
    INotificationService notifications,
    IUserPresenceService presence)
    : IRequestHandler<MarkNotificationsReadCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        if (request.NotificationIds is { Count: > 0 })
        {
            foreach (var id in request.NotificationIds.Distinct())
                await NotificationLifecycle.EnsureRecipientAsync(connection, id, request.UserId, cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Notifications SET IsRead = 1, ReadDate = GETUTCDATE(), UpdatedAt = GETUTCDATE()
                WHERE UserId = @UserId AND Id IN @Ids AND IsRead = 0
                """,
                new { request.UserId, Ids = request.NotificationIds },
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE NotificationRecipients SET IsRead = 1, ReadAt = GETUTCDATE()
                WHERE UserId = @UserId AND NotificationId IN @Ids AND IsRead = 0 AND IsDeleted = 0
                """,
                new { request.UserId, Ids = request.NotificationIds },
                cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Notifications SET IsRead = 1, ReadDate = GETUTCDATE(), UpdatedAt = GETUTCDATE()
                WHERE UserId = @UserId AND IsRead = 0 AND IsDeleted = 0 AND ISNULL(IsArchived,0) = 0
                """,
                new { request.UserId },
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE NotificationRecipients SET IsRead = 1, ReadAt = GETUTCDATE()
                WHERE UserId = @UserId AND IsRead = 0 AND IsDeleted = 0 AND IsArchived = 0
                """,
                new { request.UserId },
                cancellationToken: cancellationToken));
        }

        await presence.MarkReadAsync(request.UserId, cancellationToken);
        await notifications.InvalidateUnreadCacheAsync(request.UserId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Notifications marked as read.");
    }
}

public record DeleteNotificationCommand(int UserId, int NotificationId) : IRequest<ApiResponse<bool>>;

public class DeleteNotificationCommandHandler(
    IDbConnectionFactory dbFactory,
    INotificationService notifications)
    : IRequestHandler<DeleteNotificationCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        var result = await NotificationLifecycle.SoftDeleteAsync(
            dbFactory, notifications, request.UserId, [request.NotificationId], cancellationToken);
        return result > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Notification deleted.")
            : ApiResponse<bool>.FailResponse("Notification not found.");
    }
}

public record BulkSoftDeleteNotificationsCommand(int UserId, List<int> Ids) : IRequest<ApiResponse<int>>;

public class BulkSoftDeleteNotificationsCommandHandler(
    IDbConnectionFactory dbFactory,
    INotificationService notifications)
    : IRequestHandler<BulkSoftDeleteNotificationsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(BulkSoftDeleteNotificationsCommand request, CancellationToken cancellationToken)
    {
        var count = await NotificationLifecycle.SoftDeleteAsync(
            dbFactory, notifications, request.UserId, request.Ids, cancellationToken);
        return ApiResponse<int>.SuccessResponse(count, $"Deleted {count} notification(s).");
    }
}

/// <summary>Soft-delete every inbox (or archived) notification for the current user.</summary>
public record SoftDeleteAllNotificationsCommand(int UserId, string Scope = "inbox")
    : IRequest<ApiResponse<int>>;

public class SoftDeleteAllNotificationsCommandHandler(
    IDbConnectionFactory dbFactory,
    INotificationService notifications)
    : IRequestHandler<SoftDeleteAllNotificationsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        SoftDeleteAllNotificationsCommand request, CancellationToken cancellationToken)
    {
        var scope = (request.Scope ?? "inbox").Trim().ToLowerInvariant();
        var count = scope switch
        {
            "archived" => await NotificationLifecycle.SoftDeleteAllAsync(
                dbFactory, notifications, request.UserId, archivedOnly: true, cancellationToken),
            "trash" => await NotificationLifecycle.EmptyTrashAsync(
                dbFactory, notifications, request.UserId, cancellationToken),
            _ => await NotificationLifecycle.SoftDeleteAllAsync(
                dbFactory, notifications, request.UserId, archivedOnly: false, cancellationToken)
        };

        var label = scope switch
        {
            "archived" => "archived notification(s) moved to Trash",
            "trash" => "notification(s) permanently removed from Trash",
            _ => "notification(s) moved to Trash"
        };
        return ApiResponse<int>.SuccessResponse(count, $"{count} {label}.");
    }
}

public record ArchiveNotificationsCommand(int UserId, List<int> Ids) : IRequest<ApiResponse<int>>;

public class ArchiveNotificationsCommandHandler(
    IDbConnectionFactory dbFactory,
    INotificationService notifications)
    : IRequestHandler<ArchiveNotificationsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(ArchiveNotificationsCommand request, CancellationToken cancellationToken)
    {
        var count = await NotificationLifecycle.ArchiveAsync(
            dbFactory, notifications, request.UserId, request.Ids, cancellationToken);
        return ApiResponse<int>.SuccessResponse(count, $"Archived {count} notification(s).");
    }
}

public record RestoreNotificationsCommand(int UserId, List<int> Ids) : IRequest<ApiResponse<int>>;

public class RestoreNotificationsCommandHandler(
    IDbConnectionFactory dbFactory,
    INotificationService notifications)
    : IRequestHandler<RestoreNotificationsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(RestoreNotificationsCommand request, CancellationToken cancellationToken)
    {
        var count = await NotificationLifecycle.RestoreAsync(
            dbFactory, notifications, request.UserId, request.Ids, cancellationToken);
        return ApiResponse<int>.SuccessResponse(count, $"Restored {count} notification(s).");
    }
}

public record UpsertNotificationRetentionCommand(int TenantId, NotificationRetentionDto Policy)
    : IRequest<ApiResponse<NotificationRetentionDto>>;

public class UpsertNotificationRetentionCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpsertNotificationRetentionCommand, ApiResponse<NotificationRetentionDto>>
{
    public async Task<ApiResponse<NotificationRetentionDto>> Handle(
        UpsertNotificationRetentionCommand request, CancellationToken cancellationToken)
    {
        var policy = new NotificationRetentionPolicy
        {
            ReadArchiveDays = request.Policy.ReadArchiveDays,
            ArchivedDeleteDays = request.Policy.ArchivedDeleteDays,
            FailedDeleteDays = request.Policy.FailedDeleteDays,
            DraftDeleteDays = request.Policy.DraftDeleteDays,
            OperationalDeleteDays = request.Policy.OperationalDeleteDays,
            MaintenanceDeleteDays = request.Policy.MaintenanceDeleteDays,
            ComplianceDeleteDays = request.Policy.ComplianceDeleteDays,
            CriticalNeverDelete = request.Policy.CriticalNeverDelete,
            SecurityDeleteDays = request.Policy.SecurityDeleteDays
        };

        using var connection = dbFactory.CreateConnection();
        foreach (var (key, value) in policy.ToDictionary())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM PlatformSettings WHERE TenantId = @TenantId AND Category = @Category AND [Key] = @Key)
                    UPDATE PlatformSettings SET Value = @Value, UpdatedAt = GETUTCDATE(), IsActive = 1
                    WHERE TenantId = @TenantId AND Category = @Category AND [Key] = @Key;
                ELSE
                    INSERT INTO PlatformSettings (TenantId, Category, [Key], Value)
                    VALUES (@TenantId, @Category, @Key, @Value);
                """,
                new
                {
                    request.TenantId,
                    Category = NotificationRetention.SettingsCategory,
                    Key = key,
                    Value = value
                },
                cancellationToken: cancellationToken));
        }

        return ApiResponse<NotificationRetentionDto>.SuccessResponse(request.Policy, "Retention policy saved.");
    }
}

public record RunNotificationRetentionCleanupCommand(int TenantId) : IRequest<ApiResponse<NotificationRetentionEstimateDto>>;

public class RunNotificationRetentionCleanupCommandHandler(INotificationRetentionService retention)
    : IRequestHandler<RunNotificationRetentionCleanupCommand, ApiResponse<NotificationRetentionEstimateDto>>
{
    public async Task<ApiResponse<NotificationRetentionEstimateDto>> Handle(
        RunNotificationRetentionCleanupCommand request, CancellationToken cancellationToken)
    {
        var result = await retention.RunCleanupAsync(request.TenantId, cancellationToken);
        return ApiResponse<NotificationRetentionEstimateDto>.SuccessResponse(result, "Cleanup completed.");
    }
}

/// <summary>Shared recipient-scoped soft delete / archive / restore helpers.</summary>
internal static class NotificationLifecycle
{
    public static async Task EnsureRecipientAsync(
        System.Data.IDbConnection connection, int notificationId, int userId, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM NotificationRecipients WHERE NotificationId = @NotificationId AND UserId = @UserId)
            INSERT INTO NotificationRecipients
                (NotificationId, UserId, DeliveryStatus, IsRead, CreatedAt, IsArchived, IsDeleted)
            SELECT n.Id, @UserId, ISNULL(n.DeliveryStatus, 'Pending'), n.IsRead, GETUTCDATE(), 0, 0
            FROM Notifications n
            WHERE n.Id = @NotificationId
              AND (
                    n.UserId = @UserId
                 OR n.UserId IS NULL
                 OR EXISTS (
                        SELECT 1 FROM NotificationRecipients rx
                        WHERE rx.NotificationId = n.Id AND rx.UserId = @UserId)
              );
            """,
            new { NotificationId = notificationId, UserId = userId },
            cancellationToken: ct));
    }

    public static async Task EnsureRecipientsAsync(
        System.Data.IDbConnection connection, int userId, IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO NotificationRecipients
                (NotificationId, UserId, DeliveryStatus, IsRead, CreatedAt, IsArchived, IsDeleted)
            SELECT n.Id, @UserId, ISNULL(n.DeliveryStatus, 'Pending'), n.IsRead, GETUTCDATE(), 0, 0
            FROM Notifications n
            WHERE n.Id IN @Ids
              AND (
                    n.UserId = @UserId
                 OR n.UserId IS NULL
              )
              AND NOT EXISTS (
                    SELECT 1 FROM NotificationRecipients r
                    WHERE r.NotificationId = n.Id AND r.UserId = @UserId);
            """,
            new { UserId = userId, Ids = ids },
            cancellationToken: ct));
    }

    public static async Task<int> SoftDeleteAllAsync(
        IDbConnectionFactory dbFactory,
        INotificationService notifications,
        int userId,
        bool archivedOnly,
        CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();

        // Ensure recipient rows for owned + global notifications still in inbox/archive
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO NotificationRecipients
                (NotificationId, UserId, DeliveryStatus, IsRead, CreatedAt, IsArchived, IsDeleted)
            SELECT n.Id, @UserId, ISNULL(n.DeliveryStatus, 'Pending'), n.IsRead, GETUTCDATE(),
                   ISNULL(n.IsArchived, 0), 0
            FROM Notifications n
            WHERE (n.UserId = @UserId OR n.UserId IS NULL)
              AND ISNULL(n.IsDeleted, 0) = 0
              AND (
                    (@ArchivedOnly = 1 AND ISNULL(n.IsArchived, 0) = 1)
                 OR (@ArchivedOnly = 0 AND ISNULL(n.IsArchived, 0) = 0)
              )
              AND NOT EXISTS (
                    SELECT 1 FROM NotificationRecipients r
                    WHERE r.NotificationId = n.Id AND r.UserId = @UserId);
            """,
            new { UserId = userId, ArchivedOnly = archivedOnly ? 1 : 0 },
            cancellationToken: ct));

        var recipientRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE NotificationRecipients SET
                IsDeleted = 1, DeletedAt = GETUTCDATE(), DeletedBy = @UserId,
                IsArchived = 0, ArchivedAt = NULL
            WHERE UserId = @UserId
              AND ISNULL(IsDeleted, 0) = 0
              AND (
                    (@ArchivedOnly = 1 AND ISNULL(IsArchived, 0) = 1)
                 OR (@ArchivedOnly = 0 AND ISNULL(IsArchived, 0) = 0)
              );
            """,
            new { UserId = userId, ArchivedOnly = archivedOnly ? 1 : 0 },
            cancellationToken: ct));

        var ownerRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET
                IsDeleted = 1, IsArchived = 0, ArchivedAt = NULL, UpdatedAt = GETUTCDATE()
            WHERE UserId = @UserId
              AND ISNULL(IsDeleted, 0) = 0
              AND (
                    (@ArchivedOnly = 1 AND ISNULL(IsArchived, 0) = 1)
                 OR (@ArchivedOnly = 0 AND ISNULL(IsArchived, 0) = 0)
              );
            """,
            new { UserId = userId, ArchivedOnly = archivedOnly ? 1 : 0 },
            cancellationToken: ct));

        var affected = Math.Max(recipientRows, ownerRows);
        if (affected > 0)
            await notifications.InvalidateUnreadCacheAsync(userId, ct);
        return affected;
    }

    /// <summary>Permanently remove the current user's trash (soft-deleted recipient rows + orphaned owner rows).</summary>
    public static async Task<int> EmptyTrashAsync(
        IDbConnectionFactory dbFactory,
        INotificationService notifications,
        int userId,
        CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();

        var recipientDeleted = await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM NotificationRecipients
            WHERE UserId = @UserId AND ISNULL(IsDeleted, 0) = 1;
            """,
            new { UserId = userId },
            cancellationToken: ct));

        // Owner rows in trash with no remaining recipients
        var ownerDeleted = await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM NotificationDeliveryLogs
            WHERE NotificationId IN (
                SELECT n.Id FROM Notifications n
                WHERE n.UserId = @UserId AND ISNULL(n.IsDeleted, 0) = 1
                  AND NOT EXISTS (SELECT 1 FROM NotificationRecipients r WHERE r.NotificationId = n.Id)
            );

            DELETE FROM Notifications
            WHERE UserId = @UserId AND ISNULL(IsDeleted, 0) = 1
              AND NOT EXISTS (SELECT 1 FROM NotificationRecipients r WHERE r.NotificationId = Notifications.Id);
            """,
            new { UserId = userId },
            cancellationToken: ct));

        if (recipientDeleted + ownerDeleted > 0)
            await notifications.InvalidateUnreadCacheAsync(userId, ct);

        return recipientDeleted + ownerDeleted;
    }

    public static async Task<int> SoftDeleteAsync(
        IDbConnectionFactory dbFactory,
        INotificationService notifications,
        int userId,
        List<int> ids,
        CancellationToken ct)
    {
        var distinct = ids.Where(id => id > 0).Distinct().ToList();
        if (distinct.Count == 0) return 0;

        using var connection = dbFactory.CreateConnection();
        await EnsureRecipientsAsync(connection, userId, distinct, ct);

        var recipientRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE NotificationRecipients SET
                IsDeleted = 1, DeletedAt = GETUTCDATE(), DeletedBy = @UserId,
                IsArchived = 0, ArchivedAt = NULL
            WHERE UserId = @UserId AND NotificationId IN @Ids AND ISNULL(IsDeleted, 0) = 0
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: ct));

        // Legacy / owner rows without a working recipient path
        var ownerRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET
                IsDeleted = 1, IsArchived = 0, ArchivedAt = NULL, UpdatedAt = GETUTCDATE()
            WHERE Id IN @Ids AND UserId = @UserId AND ISNULL(IsDeleted, 0) = 0
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: ct));

        var affected = Math.Max(recipientRows, ownerRows);
        if (affected > 0)
            await notifications.InvalidateUnreadCacheAsync(userId, ct);

        return affected > 0 ? distinct.Count : 0;
    }

    public static async Task<int> ArchiveAsync(
        IDbConnectionFactory dbFactory,
        INotificationService notifications,
        int userId,
        List<int> ids,
        CancellationToken ct)
    {
        var distinct = ids.Where(id => id > 0).Distinct().ToList();
        if (distinct.Count == 0) return 0;

        using var connection = dbFactory.CreateConnection();
        await EnsureRecipientsAsync(connection, userId, distinct, ct);

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE NotificationRecipients SET
                IsArchived = 1, ArchivedAt = GETUTCDATE(),
                IsDeleted = 0, DeletedAt = NULL, DeletedBy = NULL
            WHERE UserId = @UserId AND NotificationId IN @Ids
              AND ISNULL(IsDeleted, 0) = 0 AND ISNULL(IsArchived, 0) = 0
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET IsArchived = 1, ArchivedAt = GETUTCDATE(), IsDeleted = 0, UpdatedAt = GETUTCDATE()
            WHERE UserId = @UserId AND Id IN @Ids
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: ct));

        if (rows > 0)
            await notifications.InvalidateUnreadCacheAsync(userId, ct);
        return rows > 0 ? distinct.Count : rows;
    }

    public static async Task<int> RestoreAsync(
        IDbConnectionFactory dbFactory,
        INotificationService notifications,
        int userId,
        List<int> ids,
        CancellationToken ct)
    {
        var distinct = ids.Where(id => id > 0).Distinct().ToList();
        if (distinct.Count == 0) return 0;

        using var connection = dbFactory.CreateConnection();
        await EnsureRecipientsAsync(connection, userId, distinct, ct);

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE NotificationRecipients SET
                IsDeleted = 0, DeletedAt = NULL, DeletedBy = NULL,
                IsArchived = 0, ArchivedAt = NULL
            WHERE UserId = @UserId AND NotificationId IN @Ids
              AND (ISNULL(IsDeleted, 0) = 1 OR ISNULL(IsArchived, 0) = 1)
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET IsDeleted = 0, IsArchived = 0, ArchivedAt = NULL, UpdatedAt = GETUTCDATE()
            WHERE UserId = @UserId AND Id IN @Ids
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: ct));

        if (rows > 0)
            await notifications.InvalidateUnreadCacheAsync(userId, ct);
        return rows > 0 ? distinct.Count : rows;
    }
}


public record CreateNotificationCommand(int ActorUserId, CreateNotificationRequest Request)
    : IRequest<ApiResponse<int>>;

public class CreateNotificationCommandHandler(INotificationService notifications)
    : IRequestHandler<CreateNotificationCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateNotificationCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        if (r.Broadcast)
        {
            var channels = r.Channels is { Count: > 0 }
                ? r.Channels
                : [NotificationChannels.Normalize(r.Channel)];

            await notifications.CreateForAllChannelsAsync(
                r.Title, r.Message, r.Type, channels, r.Priority, r.Module, r.ReferenceId, r.TemplateKey,
                cancellationToken: cancellationToken);
            return ApiResponse<int>.SuccessResponse(0, "Broadcast notification created.");
        }

        var id = await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
            r.UserId ?? request.ActorUserId,
            r.Title,
            r.Message,
            r.Type,
            r.ReferenceId,
            r.Priority,
            NotificationChannels.Normalize(r.Channel),
            r.RecipientType,
            r.TemplateKey,
            r.SendNow,
            Module: r.Module), cancellationToken);

        return ApiResponse<int>.SuccessResponse(id, "Notification created.");
    }
}

public record SendNotificationCommand(int NotificationId, int UserId) : IRequest<ApiResponse<bool>>;

public class SendNotificationCommandHandler(INotificationService notifications)
    : IRequestHandler<SendNotificationCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        await notifications.DispatchByIdAsync(request.NotificationId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Notification send requested.");
    }
}

public record BulkNotificationCommand(BulkNotificationRequest Request) : IRequest<ApiResponse<int>>;

public class BulkNotificationCommandHandler(INotificationService notifications)
    : IRequestHandler<BulkNotificationCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(BulkNotificationCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        var channels = r.Channels is { Count: > 0 }
            ? r.Channels
            : [NotificationChannels.InApp];

        var userIds = r.UserIds ?? [];
        if (userIds.Count == 0)
        {
            await notifications.CreateForAllChannelsAsync(
                r.Title, r.Message, r.Type, channels, r.Priority, r.Module, r.ReferenceId, r.TemplateKey,
                cancellationToken: cancellationToken);
            return ApiResponse<int>.SuccessResponse(0, "Bulk broadcast created.");
        }

        var count = 0;
        foreach (var userId in userIds)
        {
            foreach (var channel in channels)
            {
                await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
                    userId, r.Title, r.Message, r.Type, r.ReferenceId, r.Priority,
                    NotificationChannels.Normalize(channel), TemplateKey: r.TemplateKey,
                    SendNow: r.SendNow, Module: r.Module),
                    cancellationToken);
                count++;
            }
        }

        return ApiResponse<int>.SuccessResponse(count, $"Created {count} notification(s).");
    }
}

public record SendManualMessageCommand(int ActorUserId, SendManualMessageRequest Request)
    : IRequest<ApiResponse<int>>;

public class SendManualMessageCommandHandler(
    IDbConnectionFactory dbFactory,
    INotificationService notifications)
    : IRequestHandler<SendManualMessageCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SendManualMessageCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        if (string.IsNullOrWhiteSpace(r.Subject) || string.IsNullOrWhiteSpace(r.Body))
            return ApiResponse<int>.FailResponse("Subject and message are required.");

        var channels = (r.Channels is { Count: > 0 } ? r.Channels : [NotificationChannels.Email])
            .Select(NotificationChannels.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var priority = Math.Clamp(r.Priority, 1, 4);
        var userIds = new HashSet<int>(r.RecipientUserIds ?? []);

        using var connection = dbFactory.CreateConnection();

        if (!string.IsNullOrWhiteSpace(r.Role))
        {
            if (!Enum.TryParse<UserRole>(r.Role, ignoreCase: true, out var roleEnum))
                return ApiResponse<int>.FailResponse($"Unknown role '{r.Role}'.");

            var roleUsers = await connection.QueryAsync<int>(new CommandDefinition("""
                SELECT Id FROM Users
                WHERE IsDeleted = 0 AND IsActive = 1 AND Role = @Role
                """, new { Role = (int)roleEnum }, cancellationToken: cancellationToken));
            foreach (var id in roleUsers)
                userIds.Add(id);
        }

        var customEmails = (r.EmailAddresses ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (userIds.Count == 0 && customEmails.Count == 0)
            return ApiResponse<int>.FailResponse("Select at least one recipient (user, role, or custom email).");

        var count = 0;

        foreach (var userId in userIds)
        {
            foreach (var channel in channels)
            {
                await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
                    userId,
                    r.Subject.Trim(),
                    r.Body.Trim(),
                    NotificationType.BookingCreated,
                    Priority: priority,
                    Channel: channel,
                    TemplateKey: r.TemplateKey,
                    SendNow: r.SendNow,
                    Module: "Communication",
                    RecipientType: "Manual"), cancellationToken);
                count++;
            }
        }

        // Custom emails: Email channel only (no user inbox unless matched)
        if (channels.Contains(NotificationChannels.Email, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var email in customEmails)
            {
                var matchedUserId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                    "SELECT TOP 1 Id FROM Users WHERE Email = @Email AND IsDeleted = 0",
                    new { Email = email }, cancellationToken: cancellationToken));

                await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
                    matchedUserId,
                    r.Subject.Trim(),
                    r.Body.Trim(),
                    NotificationType.BookingCreated,
                    Priority: priority,
                    Channel: NotificationChannels.Email,
                    TemplateKey: r.TemplateKey,
                    SendNow: r.SendNow,
                    Module: "Communication",
                    RecipientType: "Manual",
                    Email: email), cancellationToken);
                count++;
            }
        }

        return ApiResponse<int>.SuccessResponse(count, $"Sent {count} message(s).");
    }
}

public record UpsertNotificationTemplateCommand(UpsertNotificationTemplateRequest Request, int? Id = null)
    : IRequest<ApiResponse<int>>;

public class UpsertNotificationTemplateCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpsertNotificationTemplateCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(UpsertNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        using var connection = dbFactory.CreateConnection();

        if (request.Id is int id)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE NotificationTemplates SET
                    TemplateKey = @TemplateKey, TemplateName = @TemplateName,
                    Subject = @Subject, Body = @Body, Channel = @Channel,
                    IsActive = @IsActive, Language = @Language, Variables = @Variables,
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND IsDeleted = 0
                """,
                new
                {
                    Id = id, r.TemplateKey, r.TemplateName, r.Subject, r.Body, r.Channel,
                    r.IsActive, Language = string.IsNullOrWhiteSpace(r.Language) ? "en" : r.Language,
                    r.Variables
                },
                cancellationToken: cancellationToken));
            return ApiResponse<int>.SuccessResponse(id, "Template updated.");
        }

        var newId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO NotificationTemplates
                (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, Language, Variables, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES (@TemplateKey, @TemplateName, @Subject, @Body, @Channel, @IsActive, @Language, @Variables, GETUTCDATE(), 0)
            """,
            new
            {
                r.TemplateKey, r.TemplateName, r.Subject, r.Body, r.Channel, r.IsActive,
                Language = string.IsNullOrWhiteSpace(r.Language) ? "en" : r.Language,
                r.Variables
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(newId, "Template created.");
    }
}

public record UpsertNotificationPreferencesCommand(int UserId, NotificationPreferencesDto Preferences)
    : IRequest<ApiResponse<NotificationPreferencesDto>>;

public class UpsertNotificationPreferencesCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpsertNotificationPreferencesCommand, ApiResponse<NotificationPreferencesDto>>
{
    public async Task<ApiResponse<NotificationPreferencesDto>> Handle(
        UpsertNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        var p = request.Preferences;
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE NotificationPreferences AS target
            USING (SELECT @UserId AS UserId) AS source
            ON target.UserId = source.UserId
            WHEN MATCHED THEN
                UPDATE SET
                    EmailEnabled = @EmailEnabled,
                    SmsEnabled = @SmsEnabled,
                    PushEnabled = @PushEnabled,
                    BrowserEnabled = @BrowserEnabled,
                    WhatsAppEnabled = @WhatsAppEnabled,
                    UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (UserId, EmailEnabled, SmsEnabled, PushEnabled, BrowserEnabled, WhatsAppEnabled, UpdatedAt)
                VALUES (@UserId, @EmailEnabled, @SmsEnabled, @PushEnabled, @BrowserEnabled, @WhatsAppEnabled, GETUTCDATE());
            """,
            new
            {
                request.UserId,
                p.EmailEnabled,
                p.SmsEnabled,
                p.PushEnabled,
                p.BrowserEnabled,
                p.WhatsAppEnabled
            },
            cancellationToken: cancellationToken));

        return ApiResponse<NotificationPreferencesDto>.SuccessResponse(p, "Preferences saved.");
    }
}
