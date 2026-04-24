using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Users.Commands;
using SheikhTravelSystem.Application.Features.Users.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize(Roles = "Admin")]
/// <summary>
/// Manages user administration endpoints.
/// </summary>
public class UsersController : BaseApiController
{
    /// <summary>
    /// Gets users using filter and pagination criteria.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetUsersQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>
    /// Gets a single user by identifier.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetUserByIdQuery(id)));

    /// <summary>
    /// Creates a new user account.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }

    /// <summary>
    /// Updates an existing user by identifier.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>
    /// Activates or deactivates a user account.
    /// </summary>
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateUserStatusCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>
    /// Resets a user's password and returns a temporary password.
    /// </summary>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id)
        => Ok(await Mediator.Send(new ResetUserPasswordCommand(id)));

    /// <summary>
    /// Deletes a user by identifier.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteUserCommand(id)));
}
