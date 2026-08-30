using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.Application.Features.Auth.Commands;
using SheikhTravelSystem.Application.Features.Auth.Queries;

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
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        return Ok(await Mediator.Send(command with { ClientIp = clientIp }));
    }

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

    /// <summary>
    /// Returns the authenticated user's profile (requires Bearer token).
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
        => Ok(await Mediator.Send(new GetCurrentUserQuery()));

    /// <summary>Starts a password reset email flow (always returns a generic success message).</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Completes password reset using a one-time token from email.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordWithTokenCommand command)
        => Ok(await Mediator.Send(command));
}
