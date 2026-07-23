using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Pricing.Commands;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(OperationsPermissions.BookingView)]
/// <summary>
/// Provides pricing and fare calculation endpoints.
/// </summary>
public class PricingController : BaseApiController
{
    /// <summary>
    /// Calculates booking price from the provided pricing command.
    /// </summary>
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] CalculatePriceRequest request)
        => Ok(await Mediator.Send(new CalculatePriceCommand(request)));
}
