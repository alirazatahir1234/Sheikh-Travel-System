using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Tenants.Queries;

namespace SheikhTravelSystem.API.Controllers;

[ApiController]
[Route("api/tenants")]
public class TenantsController : BaseApiController
{
    [AllowAnonymous]
    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding()
        => Ok(await Mediator.Send(new GetTenantBrandingQuery()));

    [Authorize(Roles = "Admin")]
    [HttpPost("provision")]
    public async Task<IActionResult> Provision([FromBody] ProvisionTenantCommand command)
        => Ok(await Mediator.Send(command));
}
