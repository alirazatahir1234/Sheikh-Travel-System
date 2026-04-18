using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.Application.Features.Auth.Commands;

namespace SheikhTravelSystem.API.Controllers;

[EnableRateLimiting("auth")]
public class AuthController : BaseApiController
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
        => Ok(await Mediator.Send(command));

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command)
        => Ok(await Mediator.Send(command));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
        => Ok(await Mediator.Send(new LogoutCommand()));
}
