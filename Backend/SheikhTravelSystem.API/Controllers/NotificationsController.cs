using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Notifications.Commands;
using SheikhTravelSystem.Application.Features.Notifications.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Manages user notification operations.
/// </summary>
public class NotificationsController : BaseApiController
{
    /// <summary>
    /// Gets paged notifications for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = int.Parse(User.FindFirst("userId")!.Value);
        return Ok(await Mediator.Send(new GetNotificationsQuery(userId, page, pageSize)));
    }

    /// <summary>
    /// Marks notifications as read for the current user.
    /// </summary>
    [HttpPut("read")]
    public async Task<IActionResult> MarkRead([FromBody] List<int>? notificationIds)
    {
        var userId = int.Parse(User.FindFirst("userId")!.Value);
        return Ok(await Mediator.Send(new MarkNotificationsReadCommand(userId, notificationIds)));
    }
}
