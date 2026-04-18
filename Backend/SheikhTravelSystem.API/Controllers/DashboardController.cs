using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Dashboard.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class DashboardController : BaseApiController
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
        => Ok(await Mediator.Send(new GetDashboardSummaryQuery()));
}
