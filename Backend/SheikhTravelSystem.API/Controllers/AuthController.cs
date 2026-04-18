using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.Application.Features.Auth.Commands;

namespace SheikhTravelSystem.API.Controllers;

[EnableRateLimiting("auth")]
/// <summary>
/// Handles authentication and token lifecycle endpoints.
/// </summary>
public class AuthController : BaseApiController
{
    /// <summary>
    /// Authenticates a user and returns access tokens.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>
    /// Refreshes access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>
    /// Invalidates the current authenticated session.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
        => Ok(await Mediator.Send(new LogoutCommand()));
}
