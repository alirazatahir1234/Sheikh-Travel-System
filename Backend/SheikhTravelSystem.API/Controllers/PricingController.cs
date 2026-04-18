using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Pricing.Commands;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class PricingController : BaseApiController
{
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] CalculatePriceCommand command)
        => Ok(await Mediator.Send(command));
}
