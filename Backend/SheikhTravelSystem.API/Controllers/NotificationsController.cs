using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Notifications.Commands;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;
using SheikhTravelSystem.Application.Features.Notifications.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class NotificationsController : BaseApiController
{
    private int CurrentUserId => int.Parse(User.FindFirst("userId")!.Value);

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
        [FromQuery] DateTime? toDate = null)
        => Ok(await Mediator.Send(new GetNotificationsQuery(
            CurrentUserId, page, pageSize, unreadOnly, isSent, channel, priority, search, fromDate, toDate)));

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
        => Ok(await Mediator.Send(new GetNotificationStatsQuery(CurrentUserId)));

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

    [HttpPut("read")]
    public async Task<IActionResult> MarkRead([FromBody] List<int>? notificationIds)
        => Ok(await Mediator.Send(new MarkNotificationsReadCommand(CurrentUserId, notificationIds)));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteNotificationCommand(CurrentUserId, id)));
}
