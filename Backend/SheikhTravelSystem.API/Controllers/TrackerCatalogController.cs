using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Queries;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// GPS tracker hardware catalog — brands and models used during registration.
/// </summary>
[Authorize(Roles = "Admin,Dispatcher,Accountant")]
[Route("api")]
public class TrackerCatalogController : BaseApiController
{
    [HttpGet("tracker-brands")]
    public async Task<IActionResult> GetBrands()
        => Ok(await Mediator.Send(new GetTrackerBrandsQuery()));

    [HttpGet("tracker-models")]
    public async Task<IActionResult> GetModels([FromQuery] int? brandId)
        => Ok(await Mediator.Send(new GetTrackerModelsQuery(brandId)));
}
