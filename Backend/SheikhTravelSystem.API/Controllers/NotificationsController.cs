using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Notifications.Commands;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;
using SheikhTravelSystem.Application.Features.Notifications.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class NotificationsController : BaseApiController
{
    private int CurrentUserId => int.Parse(User.FindFirst("userId")!.Value);

    [HttpGet("recipients")]
    public async Task<IActionResult> GetRecipients(
        [FromServices] IDbConnectionFactory dbFactory,
        [FromServices] IPlatformScope platformScope,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var tenantId = platformScope.TenantId;
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<NotificationRecipientDto>(
            new CommandDefinition("""
                SELECT TOP 300 Id, FullName, Email
                FROM Users
                WHERE IsDeleted = 0 AND TenantId = @TenantId
                  AND (@Search IS NULL OR FullName LIKE @Like OR Email LIKE @Like)
                ORDER BY FullName
                """,
                new
                {
                    TenantId = tenantId,
                    Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                    Like = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%"
                },
                cancellationToken: ct))).ToList();

        return Ok(ApiResponse<List<NotificationRecipientDto>>.SuccessResponse(rows));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] bool? isSent = null,
        [FromQuery] string? channel = null,
        [FromQuery] int? priority = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? module = null,
        [FromQuery] bool archived = false,
        [FromQuery] bool trash = false,
        [FromQuery] string? datePreset = null)
        => Ok(await Mediator.Send(new GetNotificationsQuery(
            CurrentUserId, page, pageSize, unreadOnly, isSent, channel, priority, search,
            fromDate, toDate, module, archived, datePreset, trash)));

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
        => Ok(await Mediator.Send(new GetNotificationStatsQuery(CurrentUserId)));

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
        => Ok(await Mediator.Send(new GetUnreadNotificationCountQuery(CurrentUserId)));

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
        => Ok(await Mediator.Send(new GetNotificationPreferencesQuery(CurrentUserId)));

    [HttpPut("preferences")]
    public async Task<IActionResult> UpsertPreferences([FromBody] NotificationPreferencesDto request)
        => Ok(await Mediator.Send(new UpsertNotificationPreferencesCommand(CurrentUserId, request)));

    [HttpGet("retention")]
    public async Task<IActionResult> GetRetention([FromServices] IPlatformScope platformScope)
        => Ok(await Mediator.Send(new GetNotificationRetentionQuery(platformScope.TenantId)));

    [HttpPut("retention")]
    public async Task<IActionResult> UpsertRetention(
        [FromBody] NotificationRetentionDto request,
        [FromServices] IPlatformScope platformScope)
        => Ok(await Mediator.Send(new UpsertNotificationRetentionCommand(platformScope.TenantId, request)));

    [HttpGet("retention/estimate")]
    public async Task<IActionResult> RetentionEstimate([FromServices] IPlatformScope platformScope)
        => Ok(await Mediator.Send(new GetNotificationRetentionEstimateQuery(platformScope.TenantId)));

    [HttpPost("retention/run")]
    public async Task<IActionResult> RunRetention([FromServices] IPlatformScope platformScope)
        => Ok(await Mediator.Send(new RunNotificationRetentionCleanupCommand(platformScope.TenantId)));

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] string? channel = null)
        => Ok(await Mediator.Send(new GetNotificationTemplatesQuery(channel)));

    [HttpPost("templates")]
    public async Task<IActionResult> UpsertTemplate([FromBody] UpsertNotificationTemplateRequest request)
        => Ok(await Mediator.Send(new UpsertNotificationTemplateCommand(request)));

    [HttpPut("templates/{id:int}")]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] UpsertNotificationTemplateRequest request)
        => Ok(await Mediator.Send(new UpsertNotificationTemplateCommand(request, id)));

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id)
        => Ok(await Mediator.Send(new GetNotificationHistoryQuery(id, CurrentUserId)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNotificationRequest request)
        => Ok(await Mediator.Send(new CreateNotificationCommand(CurrentUserId, request)));

    [HttpPost("bulk")]
    public async Task<IActionResult> Bulk([FromBody] BulkNotificationRequest request)
        => Ok(await Mediator.Send(new BulkNotificationCommand(request)));

    [HttpPost("{id:int}/send")]
    public async Task<IActionResult> Send(int id)
        => Ok(await Mediator.Send(new SendNotificationCommand(id, CurrentUserId)));

    /// <summary>Admin compose — send email / multi-channel message; appears in Notification Center history.</summary>
    [HttpPost("send-email")]
    public async Task<IActionResult> SendEmail([FromBody] SendManualMessageRequest request)
        => Ok(await Mediator.Send(new SendManualMessageCommand(CurrentUserId, request)));

    /// <summary>Sends one test email via configured SMTP (SpaceMail). Optional body.to overrides recipient.</summary>
    [HttpPost("test-email")]
    public async Task<IActionResult> TestEmail(
        [FromBody] TestEmailRequest? body,
        [FromServices] INotificationService notifications,
        CancellationToken ct)
    {
        var to = body?.To;
        var id = await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
            CurrentUserId,
            body?.Subject ?? "SheikhGo SMTP test",
            body?.Message ?? "If you received this message, email delivery from SheikhGo is working.",
            SheikhTravelSystem.Domain.Enums.NotificationType.TripDelayed,
            Priority: 2,
            Channel: NotificationChannels.Email,
            Module: "System",
            Email: to,
            SendNow: true), ct);

        return Ok(new
        {
            notificationId = id,
            message = id > 0
                ? "Test email dispatched. Check the inbox and API logs for 'SMTP ok' or SMTP errors."
                : "Email was skipped (preferences disabled or create failed)."
        });
    }

    [HttpPut("read")]
    public async Task<IActionResult> MarkRead([FromBody] List<int>? notificationIds)
        => Ok(await Mediator.Send(new MarkNotificationsReadCommand(CurrentUserId, notificationIds)));

    [HttpPost("archive")]
    public async Task<IActionResult> Archive([FromBody] NotificationLifecycleIdsRequest request)
        => Ok(await Mediator.Send(new ArchiveNotificationsCommand(CurrentUserId, request.Ids ?? [])));

    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] NotificationLifecycleIdsRequest request)
        => Ok(await Mediator.Send(new RestoreNotificationsCommand(CurrentUserId, request.Ids ?? [])));

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] NotificationLifecycleIdsRequest request)
        => Ok(await Mediator.Send(new BulkSoftDeleteNotificationsCommand(CurrentUserId, request.Ids ?? [])));

    /// <summary>
    /// Soft-delete all inbox notifications for the current user (scope=inbox|archived),
    /// or permanently empty Trash (scope=trash).
    /// </summary>
    [HttpPost("delete-all")]
    public async Task<IActionResult> DeleteAll([FromQuery] string scope = "inbox")
        => Ok(await Mediator.Send(new SoftDeleteAllNotificationsCommand(CurrentUserId, scope)));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteNotificationCommand(CurrentUserId, id)));
}

public record TestEmailRequest(string? To = null, string? Subject = null, string? Message = null);
