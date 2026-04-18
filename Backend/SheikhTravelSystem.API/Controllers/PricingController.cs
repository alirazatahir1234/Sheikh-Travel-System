using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Pricing.Commands;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Provides pricing and fare calculation endpoints.
/// </summary>
public class PricingController : BaseApiController
{
    /// <summary>
    /// Calculates booking price from the provided pricing command.
    /// </summary>
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] CalculatePriceCommand command)
        => Ok(await Mediator.Send(command));
}
