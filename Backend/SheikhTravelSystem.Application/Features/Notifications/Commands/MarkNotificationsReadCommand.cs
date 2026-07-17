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

public class MarkNotificationsReadCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<MarkNotificationsReadCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        if (request.NotificationIds is { Count: > 0 })
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Notifications SET IsRead = 1, UpdatedAt = GETUTCDATE() WHERE UserId = @UserId AND Id IN @Ids",
                new { request.UserId, Ids = request.NotificationIds },
                cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Notifications SET IsRead = 1, UpdatedAt = GETUTCDATE() WHERE UserId = @UserId AND IsRead = 0",
                new { request.UserId },
                cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, "Notifications marked as read.");
    }
}

public record DeleteNotificationCommand(int UserId, int NotificationId) : IRequest<ApiResponse<bool>>;

public class DeleteNotificationCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeleteNotificationCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
            WHERE Id = @NotificationId AND UserId = @UserId AND IsDeleted = 0
            """,
            new { request.NotificationId, request.UserId },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Notification deleted.")
            : ApiResponse<bool>.FailResponse("Notification not found.");
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
            await notifications.CreateForAllAsync(r.Title, r.Message, r.Type, r.ReferenceId, cancellationToken);
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
            r.SendNow), cancellationToken);

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
            await notifications.CreateForAllAsync(r.Title, r.Message, r.Type, r.ReferenceId, cancellationToken);
            return ApiResponse<int>.SuccessResponse(0, "Bulk broadcast created.");
        }

        var count = 0;
        foreach (var userId in userIds)
        {
            foreach (var channel in channels)
            {
                await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
                    userId, r.Title, r.Message, r.Type, r.ReferenceId, r.Priority,
                    NotificationChannels.Normalize(channel), TemplateKey: r.TemplateKey, SendNow: r.SendNow),
                    cancellationToken);
                count++;
            }
        }

        return ApiResponse<int>.SuccessResponse(count, $"Created {count} notification(s).");
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
                    IsActive = @IsActive, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND IsDeleted = 0
                """,
                new { Id = id, r.TemplateKey, r.TemplateName, r.Subject, r.Body, r.Channel, r.IsActive },
                cancellationToken: cancellationToken));
            return ApiResponse<int>.SuccessResponse(id, "Template updated.");
        }

        var newId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO NotificationTemplates
                (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES (@TemplateKey, @TemplateName, @Subject, @Body, @Channel, @IsActive, GETUTCDATE(), 0)
            """,
            new { r.TemplateKey, r.TemplateName, r.Subject, r.Body, r.Channel, r.IsActive },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(newId, "Template created.");
    }
}
