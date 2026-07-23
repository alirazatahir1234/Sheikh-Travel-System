using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Users.Commands;
using SheikhTravelSystem.Application.Features.Users.Queries;

namespace SheikhTravelSystem.API.Controllers;

[RequirePermission(PlatformPermissions.UsersView)]
public class UsersController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetUsersQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>Current authenticated user (enriched profile).</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
        => Ok(await Mediator.Send(new GetCurrentUserProfileQuery()));

    /// <summary>Company-scoped user list (alias of GET with tenant filter).</summary>
    [HttpGet("company")]
    public async Task<IActionResult> GetCompanyUsers([FromQuery] GetUsersQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>Self-service profile read.</summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
        => Ok(await Mediator.Send(new GetCurrentUserProfileQuery()));

    /// <summary>Company user counts for company detail strip.</summary>
    [HttpGet("company/summary")]
    public async Task<IActionResult> GetCompanySummary([FromQuery] int? tenantId = null)
        => Ok(await Mediator.Send(new GetCompanyUserSummaryQuery(tenantId)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetUserByIdQuery(id)));

    [RequirePermission(PlatformPermissions.UsersCreate)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }

    [RequirePermission(PlatformPermissions.UsersEdit)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [RequirePermission(PlatformPermissions.UsersEdit)]
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateUserStatusCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [RequirePermission(PlatformPermissions.UsersEdit)]
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id)
        => Ok(await Mediator.Send(new ResetUserPasswordCommand(id)));

    [RequirePermission(PlatformPermissions.UsersEdit)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteUserCommand(id)));
}

[Authorize]
[ApiController]
[Route("api/users")]
public class ProfileController : BaseApiController
{
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
        => Ok(await Mediator.Send(new GetCurrentUserProfileQuery()));

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileCommand command)
    {
        var userId = int.Parse(User.FindFirst("userId")!.Value);
        return Ok(await Mediator.Send(command with { UserId = userId }));
    }

    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        var userId = int.Parse(User.FindFirst("userId")!.Value);
        return Ok(await Mediator.Send(command with { UserId = userId }));
    }
}
