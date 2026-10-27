using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TripFeedback;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[Route("api/trip-feedback")]
public class TripFeedbackAdminController : BaseApiController
{
    [RequirePermission(DriverPermissions.DriverViewPerformance)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] int? driverId = null, [FromQuery] bool includeHidden = false)
        => Ok(await Mediator.Send(new ListTripClientRatingsQuery(page, pageSize, driverId, includeHidden)));

    [RequirePermission(DriverPermissions.DriverViewPerformance)]
    [HttpGet("drivers/{driverId:int}/summary")]
    public async Task<IActionResult> DriverSummary(int driverId)
        => Ok(await Mediator.Send(new GetDriverClientRatingSummaryQuery(driverId)));

    [RequirePermission(DriverPermissions.DriverManage)]
    [HttpPatch("{ratingId:int}/visibility")]
    public async Task<IActionResult> SetVisibility(int ratingId, [FromBody] SetTripRatingVisibilityRequest body)
        => Ok(await Mediator.Send(new SetTripRatingVisibilityCommand(ratingId, body.Hidden)));

    [RequirePermission(DriverPermissions.DriverManage)]
    [HttpPatch("drivers/{driverId:int}/public-profile")]
    public async Task<IActionResult> SetPublicProfile(int driverId, [FromBody] SetDriverPublicRatingRequest body)
        => Ok(await Mediator.Send(new SetDriverPublicRatingOptOutCommand(driverId, body.OptOut)));
}

[AllowAnonymous]
[ApiController]
[Route("api/public/trip-feedback")]
[EnableRateLimiting("public")]
public class TripFeedbackPublicController : BaseApiController
{
    [HttpGet("{token}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetForm(string token)
    {
        var result = await Mediator.Send(new GetTripFeedbackFormQuery(token));
        if (!result.Success)
            return NotFound(result);
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Robots-Tag"] = "noindex";
        return Ok(result);
    }

    [HttpPost("{token}")]
    public async Task<IActionResult> Submit(string token, [FromBody] SubmitTripFeedbackRequest body)
    {
        var result = await Mediator.Send(new SubmitTripFeedbackCommand(token, body));
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
