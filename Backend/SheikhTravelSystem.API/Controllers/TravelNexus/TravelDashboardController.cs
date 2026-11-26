using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Dashboard.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>TravelNexus dashboard: pipelines (all time), attention counts (as of today), receivables / payables, trends, upcoming deadlines.</summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.DashboardView)]
[Route("api/travel/dashboard")]
public class TravelDashboardController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await Mediator.Send(new GetTravelDashboardQuery()));

    /// <summary>Deadlines in the next <paramref name="days"/> days (1–90), from the reminder candidate queries.</summary>
    [HttpGet("upcoming")]
    public async Task<IActionResult> GetUpcoming([FromQuery] int days = 14) => Ok(await Mediator.Send(new GetTravelUpcomingQuery(days)));

    /// <summary>Margin for travel this month and last month (sensitive).</summary>
    [RequirePermission(TravelPermissions.ProfitabilityView)]
    [HttpGet("profit")]
    public async Task<IActionResult> GetProfit() => Ok(await Mediator.Send(new GetTravelDashboardProfitQuery()));
}
