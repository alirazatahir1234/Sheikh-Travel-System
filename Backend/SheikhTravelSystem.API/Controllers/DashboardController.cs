using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Dashboard.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Provides dashboard summary endpoints.
/// </summary>
public class DashboardController : BaseApiController
{
    /// <summary>
    /// Gets the current dashboard summary.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
        => Ok(await Mediator.Send(new GetDashboardSummaryQuery()));
}
