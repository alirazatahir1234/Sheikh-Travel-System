using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Notifications.Commands;
using SheikhTravelSystem.Application.Features.Notifications.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class NotificationsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = int.Parse(User.FindFirst("userId")!.Value);
        return Ok(await Mediator.Send(new GetNotificationsQuery(userId, page, pageSize)));
    }

    [HttpPut("read")]
    public async Task<IActionResult> MarkRead([FromBody] List<int>? notificationIds)
    {
        var userId = int.Parse(User.FindFirst("userId")!.Value);
        return Ok(await Mediator.Send(new MarkNotificationsReadCommand(userId, notificationIds)));
    }
}
