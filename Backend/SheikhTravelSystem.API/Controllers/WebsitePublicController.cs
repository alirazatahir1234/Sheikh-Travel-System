using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.Application.Features.Website.Queries;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>Anonymous public website CMS read APIs for sheikhgo.com.</summary>
[Route("api/website/public")]
[EnableRateLimiting("public")]
[AllowAnonymous]
public class WebsitePublicController : BaseApiController
{
    [HttpGet("home")]
    public async Task<IActionResult> GetHome()
        => Ok(await Mediator.Send(new GetPublicHomeQuery()));

    [HttpGet("pages/{slug}")]
    public async Task<IActionResult> GetPage(string slug)
        => Ok(await Mediator.Send(new GetPublicPageQuery(slug)));

    [HttpGet("features")]
    public async Task<IActionResult> GetFeatures()
        => Ok(await Mediator.Send(new GetPublicFeaturesQuery()));

    [HttpGet("legal/{docType}")]
    public async Task<IActionResult> GetLegal(string docType)
        => Ok(await Mediator.Send(new GetPublicLegalQuery(docType)));

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
        => Ok(await Mediator.Send(new GetPublicSettingsQuery()));
}
