using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// Margins in the reporting currency: net invoiced revenue − supplier cost (billed, else estimated) − agent commission, per booking
/// file, visa case or group, or rolled up by agent / branch. Amounts without an exchange rate are excluded and reported.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.ProfitabilityView)]
[Route("api/travel/profitability")]
public class TravelProfitabilityController : BaseApiController
{
    /// <summary>dimension: Booking | VisaCase | Group | Agent | Branch. travelFrom and travelTo are required; branchId, agentUserId optional.</summary>
    [HttpGet]
    public async Task<IActionResult> GetReport([FromQuery] string dimension, [FromQuery] DateTime? travelFrom, [FromQuery] DateTime? travelTo,
        [FromQuery] int? branchId, [FromQuery] int? agentUserId)
        => Ok(await Mediator.Send(new GetTravelProfitabilityQuery(dimension, travelFrom, travelTo, branchId, agentUserId)));

    /// <summary>contextType: Booking | VisaCase | Group.</summary>
    [HttpGet("{contextType}/{contextId:int}")]
    public async Task<IActionResult> GetContext(string contextType, int contextId)
        => Ok(await Mediator.Send(new GetTravelContextProfitQuery(contextType, contextId)));
}
