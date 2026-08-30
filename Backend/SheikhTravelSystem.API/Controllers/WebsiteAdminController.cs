using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Website.Commands;
using SheikhTravelSystem.Application.Features.Website.Queries;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>Authenticated Website CMS admin APIs.</summary>
[Authorize]
[RequirePermission(WebsitePermissions.View)]
[Route("api/website")]
public class WebsiteAdminController : BaseApiController
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
        => Ok(await Mediator.Send(new GetWebsiteDashboardQuery()));

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
        => Ok(await Mediator.Send(new GetWebsiteSettingsQuery()));

    [RequirePermission(WebsitePermissions.Settings)]
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateWebsiteSettingsCommand command)
        => Ok(await Mediator.Send(command));

    [HttpGet("pages")]
    public async Task<IActionResult> GetPages()
        => Ok(await Mediator.Send(new GetWebsitePagesQuery()));

    [RequirePermission(WebsitePermissions.Edit)]
    [HttpPut("pages/{id:int}")]
    public async Task<IActionResult> UpdatePage(int id, [FromBody] UpdateWebsitePageBody body)
        => Ok(await Mediator.Send(new UpdateWebsitePageCommand(
            id, body.Title, body.Description, body.MetaTitle, body.MetaDescription, body.OgImage, body.Status)));

    [RequirePermission(WebsitePermissions.Publish)]
    [HttpPost("pages/{id:int}/publish")]
    public async Task<IActionResult> PublishPage(int id)
        => Ok(await Mediator.Send(new PublishWebsitePageCommand(id)));

    [HttpGet("pages/{pageId:int}/sections")]
    public async Task<IActionResult> GetPageSections(int pageId)
        => Ok(await Mediator.Send(new GetWebsitePageSectionsQuery(pageId)));

    [HttpGet("home/sections")]
    public async Task<IActionResult> GetHomeSections()
        => Ok(await Mediator.Send(new GetWebsiteHomeSectionsQuery()));

    [RequirePermission(WebsitePermissions.Edit)]
    [HttpPost("sections")]
    public async Task<IActionResult> UpsertSection([FromBody] UpsertWebsiteSectionCommand command)
        => Ok(await Mediator.Send(command));

    [RequirePermission(WebsitePermissions.Edit)]
    [HttpPut("sections/{id:int}")]
    public async Task<IActionResult> UpdateSection(int id, [FromBody] UpsertWebsiteSectionCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [RequirePermission(WebsitePermissions.Edit)]
    [HttpDelete("sections/{id:int}")]
    public async Task<IActionResult> DeleteSection(int id)
        => Ok(await Mediator.Send(new DeleteWebsiteSectionCommand(id)));

    [RequirePermission(WebsitePermissions.Publish)]
    [HttpPost("sections/{id:int}/publish")]
    public async Task<IActionResult> PublishSection(int id)
        => Ok(await Mediator.Send(new PublishWebsiteSectionCommand(id)));

    [HttpGet("features")]
    public async Task<IActionResult> GetFeatures()
        => Ok(await Mediator.Send(new GetWebsiteFeaturesQuery()));

    [RequirePermission(WebsitePermissions.Edit)]
    [HttpPost("features")]
    public async Task<IActionResult> UpsertFeature([FromBody] UpsertWebsiteFeatureCommand command)
        => Ok(await Mediator.Send(command));

    [RequirePermission(WebsitePermissions.Edit)]
    [HttpPut("features/{id:int}")]
    public async Task<IActionResult> UpdateFeature(int id, [FromBody] UpsertWebsiteFeatureCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [RequirePermission(WebsitePermissions.Edit)]
    [HttpDelete("features/{id:int}")]
    public async Task<IActionResult> DeleteFeature(int id)
        => Ok(await Mediator.Send(new DeleteWebsiteFeatureCommand(id)));

    [RequirePermission(WebsitePermissions.Publish)]
    [HttpPost("features/{id:int}/publish")]
    public async Task<IActionResult> PublishFeature(int id)
        => Ok(await Mediator.Send(new PublishWebsiteFeatureCommand(id)));

    [HttpGet("legal")]
    public async Task<IActionResult> GetLegal([FromQuery] string? docType = null)
        => Ok(await Mediator.Send(new GetWebsiteLegalQuery(docType)));

    [RequirePermission(WebsitePermissions.Legal)]
    [HttpPut("legal/{docType}")]
    public async Task<IActionResult> UpdateLegal(string docType, [FromBody] UpdateWebsiteLegalBody body)
        => Ok(await Mediator.Send(new UpdateWebsiteLegalCommand(docType, body.Title, body.Content, body.Version)));

    [RequirePermission(WebsitePermissions.Publish)]
    [HttpPost("legal/{docType}/publish")]
    public async Task<IActionResult> PublishLegal(string docType)
        => Ok(await Mediator.Send(new PublishWebsiteLegalCommand(docType)));

    [HttpGet("media")]
    public async Task<IActionResult> GetMedia()
        => Ok(await Mediator.Send(new GetWebsiteMediaQuery()));

    [RequirePermission(WebsitePermissions.Media)]
    [HttpPost("media")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadMedia(IFormFile file, [FromForm] string? altText = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.FailResponse("File is required."));

        await using var stream = file.OpenReadStream();
        return Ok(await Mediator.Send(new UploadWebsiteMediaCommand(
            stream,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            altText,
            file.Length)));
    }

    [RequirePermission(WebsitePermissions.Media)]
    [HttpDelete("media/{id:int}")]
    public async Task<IActionResult> DeleteMedia(int id)
        => Ok(await Mediator.Send(new DeleteWebsiteMediaCommand(id)));

    [RequirePermission(WebsitePermissions.ContactRequests)]
    [HttpGet("contact-requests")]
    public async Task<IActionResult> GetContactRequests(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new GetWebsiteContactRequestsQuery(status, page, pageSize)));

    [RequirePermission(WebsitePermissions.ContactRequests)]
    [HttpGet("contact-requests/{id:int}")]
    public async Task<IActionResult> GetContactRequest(int id)
        => Ok(await Mediator.Send(new GetWebsiteContactRequestByIdQuery(id)));

    [RequirePermission(WebsitePermissions.ContactRequests)]
    [HttpPut("contact-requests/{id:int}/status")]
    public async Task<IActionResult> UpdateContactRequestStatus(int id, [FromBody] UpdateLeadStatusBody body)
        => Ok(await Mediator.Send(new UpdateContactRequestStatusCommand(id, body.Status)));

    [RequirePermission(WebsitePermissions.DemoRequests)]
    [HttpGet("demo-requests")]
    public async Task<IActionResult> GetDemoRequests(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new GetWebsiteDemoRequestsQuery(status, page, pageSize)));

    [RequirePermission(WebsitePermissions.DemoRequests)]
    [HttpGet("demo-requests/{id:int}")]
    public async Task<IActionResult> GetDemoRequest(int id)
        => Ok(await Mediator.Send(new GetWebsiteDemoRequestByIdQuery(id)));

    [RequirePermission(WebsitePermissions.DemoRequests)]
    [HttpPut("demo-requests/{id:int}/status")]
    public async Task<IActionResult> UpdateDemoRequestStatus(int id, [FromBody] UpdateLeadStatusBody body)
        => Ok(await Mediator.Send(new UpdateDemoRequestStatusCommand(id, body.Status)));
}

public record UpdateWebsitePageBody(
    string Title,
    string? Description = null,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? OgImage = null,
    string? Status = null);

public record UpdateWebsiteLegalBody(string Title, string Content, string? Version = null);

public record UpdateLeadStatusBody(string Status);
