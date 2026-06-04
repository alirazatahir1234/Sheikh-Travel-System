using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

namespace SheikhTravelSystem.API.Controllers;

[ApiController]
[Route("api/customer-portal/auth")]
[AllowAnonymous]
[EnableRateLimiting("portal")]
public class CustomerPortalAuthController : ControllerBase
{
    private ISender? _mediator;
    private ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] PortalSendOtpRequest body)
        => Ok(await Mediator.Send(new SendPortalOtpCommand(body.Phone)));

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] PortalVerifyOtpRequest body)
        => Ok(await Mediator.Send(new VerifyPortalOtpCommand(body.Phone, body.Code, body.FullName)));
}
