using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Notifications.Commands;

public record MarkNotificationsReadCommand(int UserId, List<int>? NotificationIds = null)
    : IRequest<ApiResponse<bool>>;

public class MarkNotificationsReadCommandHandler(
    INotificationRepository notificationRepository,
    INotificationService notifications,
    IUserPresenceService presence)
    : IRequestHandler<MarkNotificationsReadCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        await notificationRepository.MarkReadAsync(
            request.UserId, request.NotificationIds, cancellationToken);

        await presence.MarkReadAsync(request.UserId, cancellationToken);
        await notifications.InvalidateUnreadCacheAsync(request.UserId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Notifications marked as read.");
    }
}

public record DeleteNotificationCommand(int UserId, int NotificationId) : IRequest<ApiResponse<bool>>;

public class DeleteNotificationCommandHandler(
    INotificationRepository notificationRepository,
    INotificationService notifications)
    : IRequestHandler<DeleteNotificationCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        var result = await notificationRepository.SoftDeleteAsync(
            request.UserId, [request.NotificationId], cancellationToken);
        if (result > 0)
            await notifications.InvalidateUnreadCacheAsync(request.UserId, cancellationToken);
        return result > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Notification deleted.")
            : ApiResponse<bool>.FailResponse("Notification not found.");
    }
}

public record BulkSoftDeleteNotificationsCommand(int UserId, List<int> Ids) : IRequest<ApiResponse<int>>;

public class BulkSoftDeleteNotificationsCommandHandler(
    INotificationRepository notificationRepository,
    INotificationService notifications)
    : IRequestHandler<BulkSoftDeleteNotificationsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(BulkSoftDeleteNotificationsCommand request, CancellationToken cancellationToken)
    {
        var count = await notificationRepository.SoftDeleteAsync(
            request.UserId, request.Ids, cancellationToken);
        if (count > 0)
            await notifications.InvalidateUnreadCacheAsync(request.UserId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(count, $"Deleted {count} notification(s).");
    }
}

/// <summary>Soft-delete every inbox (or archived) notification for the current user.</summary>
public record SoftDeleteAllNotificationsCommand(int UserId, string Scope = "inbox")
    : IRequest<ApiResponse<int>>;

public class SoftDeleteAllNotificationsCommandHandler(
    INotificationRepository notificationRepository,
    INotificationService notifications)
    : IRequestHandler<SoftDeleteAllNotificationsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        SoftDeleteAllNotificationsCommand request, CancellationToken cancellationToken)
    {
        var scope = (request.Scope ?? "inbox").Trim().ToLowerInvariant();
        var count = scope switch
        {
            "archived" => await notificationRepository.SoftDeleteAllAsync(
                request.UserId, archivedOnly: true, cancellationToken),
            "trash" => await notificationRepository.EmptyTrashAsync(
                request.UserId, cancellationToken),
            _ => await notificationRepository.SoftDeleteAllAsync(
                request.UserId, archivedOnly: false, cancellationToken)
        };

        if (count > 0)
            await notifications.InvalidateUnreadCacheAsync(request.UserId, cancellationToken);

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
    INotificationRepository notificationRepository,
    INotificationService notifications)
    : IRequestHandler<ArchiveNotificationsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(ArchiveNotificationsCommand request, CancellationToken cancellationToken)
    {
        var count = await notificationRepository.ArchiveAsync(
            request.UserId, request.Ids, cancellationToken);
        if (count > 0)
            await notifications.InvalidateUnreadCacheAsync(request.UserId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(count, $"Archived {count} notification(s).");
    }
}

public record RestoreNotificationsCommand(int UserId, List<int> Ids) : IRequest<ApiResponse<int>>;

public class RestoreNotificationsCommandHandler(
    INotificationRepository notificationRepository,
    INotificationService notifications)
    : IRequestHandler<RestoreNotificationsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(RestoreNotificationsCommand request, CancellationToken cancellationToken)
    {
        var count = await notificationRepository.RestoreAsync(
            request.UserId, request.Ids, cancellationToken);
        if (count > 0)
            await notifications.InvalidateUnreadCacheAsync(request.UserId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(count, $"Restored {count} notification(s).");
    }
}

public record UpsertNotificationRetentionCommand(int TenantId, NotificationRetentionDto Policy)
    : IRequest<ApiResponse<NotificationRetentionDto>>;

public class UpsertNotificationRetentionCommandHandler(INotificationRepository notificationRepository)
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

        await notificationRepository.UpsertRetentionSettingsAsync(
            request.TenantId, policy.ToDictionary(), cancellationToken);

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

public record CreateNotificationCommand(int ActorUserId, int TenantId, CreateNotificationRequest Request)
    : IRequest<ApiResponse<int>>;

public class CreateNotificationCommandHandler(INotificationService notifications)
    : IRequestHandler<CreateNotificationCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateNotificationCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        if (r.Broadcast)
        {
            if (!string.Equals(r.RecipientType, "SystemAnnouncement", StringComparison.OrdinalIgnoreCase))
                return ApiResponse<int>.FailResponse("Broadcast is allowed only for SystemAnnouncement recipient type.");

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
            request.TenantId,
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

public record BulkNotificationCommand(int TenantId, BulkNotificationRequest Request) : IRequest<ApiResponse<int>>;

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
            if (!string.Equals(r.Module, "SystemAnnouncement", StringComparison.OrdinalIgnoreCase))
                return ApiResponse<int>.FailResponse("Bulk broadcast is allowed only for SystemAnnouncement recipient type.");

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
                    userId, request.TenantId, r.Title, r.Message, r.Type, r.ReferenceId, r.Priority,
                    NotificationChannels.Normalize(channel), TemplateKey: r.TemplateKey,
                    SendNow: r.SendNow, Module: r.Module),
                    cancellationToken);
                count++;
            }
        }

        return ApiResponse<int>.SuccessResponse(count, $"Created {count} notification(s).");
    }
}

public record SendManualMessageCommand(int ActorUserId, int TenantId, SendManualMessageRequest Request)
    : IRequest<ApiResponse<int>>;

public class SendManualMessageCommandHandler(
    INotificationRepository notificationRepository,
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

        if (!string.IsNullOrWhiteSpace(r.Role))
        {
            if (!Enum.TryParse<UserRole>(r.Role, ignoreCase: true, out var roleEnum))
                return ApiResponse<int>.FailResponse($"Unknown role '{r.Role}'.");

            var roleUsers = await notificationRepository.GetActiveUserIdsByRoleAsync(
                request.TenantId, (int)roleEnum, cancellationToken);
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
                    request.TenantId,
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

        if (channels.Contains(NotificationChannels.Email, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var email in customEmails)
            {
                var matchedUserId = await notificationRepository.FindUserIdByEmailAsync(
                    request.TenantId, email, cancellationToken);

                await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
                    matchedUserId,
                    request.TenantId,
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

public class UpsertNotificationTemplateCommandHandler(INotificationRepository notificationRepository)
    : IRequestHandler<UpsertNotificationTemplateCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(UpsertNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        if (request.Id is int id)
        {
            await notificationRepository.UpdateTemplateAsync(id, r, cancellationToken);
            return ApiResponse<int>.SuccessResponse(id, "Template updated.");
        }

        var newId = await notificationRepository.InsertTemplateAsync(r, cancellationToken);
        return ApiResponse<int>.SuccessResponse(newId, "Template created.");
    }
}

public record UpsertNotificationPreferencesCommand(int UserId, NotificationPreferencesDto Preferences)
    : IRequest<ApiResponse<NotificationPreferencesDto>>;

public class UpsertNotificationPreferencesCommandHandler(INotificationRepository notificationRepository)
    : IRequestHandler<UpsertNotificationPreferencesCommand, ApiResponse<NotificationPreferencesDto>>
{
    public async Task<ApiResponse<NotificationPreferencesDto>> Handle(
        UpsertNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        await notificationRepository.UpsertPreferencesAsync(
            request.UserId, request.Preferences, cancellationToken);
        return ApiResponse<NotificationPreferencesDto>.SuccessResponse(request.Preferences, "Preferences saved.");
    }
}
