using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.Application.Features.PublicLeads.Commands;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>Anonymous marketing lead capture for sheikhgo.com.</summary>
[Route("api/public")]
[EnableRateLimiting("public")]
public class PublicLeadsController : BaseApiController
{
    [HttpPost("contact")]
    [AllowAnonymous]
    public async Task<IActionResult> Contact([FromBody] SubmitContactLeadCommand command)
        => Ok(await Mediator.Send(command));

    [HttpPost("request-demo")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestDemo([FromBody] SubmitDemoLeadCommand command)
        => Ok(await Mediator.Send(command));
}
