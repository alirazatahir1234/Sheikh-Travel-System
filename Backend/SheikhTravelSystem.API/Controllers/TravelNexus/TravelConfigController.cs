using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// TravelNexus configuration: countries, visa types, document types, lead sources and settings.
/// Reads need <c>Travel.View</c>; writes need <c>Travel.Settings</c>.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.View)]
[Route("api/travel/config")]
public class TravelConfigController : BaseApiController
{
    /// <summary>ISO country reference list.</summary>
    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries() => Ok(await Mediator.Send(new GetTravelCountriesQuery()));

    /// <summary>Active branches and staff of the caller's tenant (id + name) for dropdowns.</summary>
    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups() => Ok(await Mediator.Send(new GetTravelLookupsQuery()));

    /// <summary>Visa types, optionally filtered by country (ISO alpha-2) and active flag.</summary>
    [HttpGet("visa-types")]
    public async Task<IActionResult> GetVisaTypes([FromQuery] string? countryCode, [FromQuery] bool? isActive)
        => Ok(await Mediator.Send(new GetTravelVisaTypesQuery(countryCode, isActive)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPost("visa-types")]
    public async Task<IActionResult> CreateVisaType([FromBody] SaveTravelVisaTypeDto dto)
        => Created(string.Empty, await Mediator.Send(new SaveTravelVisaTypeCommand(null, dto)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPut("visa-types/{id:int}")]
    public async Task<IActionResult> UpdateVisaType(int id, [FromBody] SaveTravelVisaTypeDto dto)
        => Ok(await Mediator.Send(new SaveTravelVisaTypeCommand(id, dto)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpDelete("visa-types/{id:int}")]
    public async Task<IActionResult> DeleteVisaType(int id) => Ok(await Mediator.Send(new DeleteTravelVisaTypeCommand(id)));

    [HttpGet("document-types")]
    public async Task<IActionResult> GetDocumentTypes([FromQuery] bool? isActive)
        => Ok(await Mediator.Send(new GetTravelDocumentTypesQuery(isActive)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPost("document-types")]
    public async Task<IActionResult> CreateDocumentType([FromBody] SaveTravelDocumentTypeDto dto)
        => Created(string.Empty, await Mediator.Send(new SaveTravelDocumentTypeCommand(null, dto)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPut("document-types/{id:int}")]
    public async Task<IActionResult> UpdateDocumentType(int id, [FromBody] SaveTravelDocumentTypeDto dto)
        => Ok(await Mediator.Send(new SaveTravelDocumentTypeCommand(id, dto)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpDelete("document-types/{id:int}")]
    public async Task<IActionResult> DeleteDocumentType(int id) => Ok(await Mediator.Send(new DeleteTravelDocumentTypeCommand(id)));

    [HttpGet("lead-sources")]
    public async Task<IActionResult> GetLeadSources([FromQuery] bool? isActive)
        => Ok(await Mediator.Send(new GetTravelLeadSourcesQuery(isActive)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPost("lead-sources")]
    public async Task<IActionResult> CreateLeadSource([FromBody] SaveTravelLeadSourceDto dto)
        => Created(string.Empty, await Mediator.Send(new SaveTravelLeadSourceCommand(null, dto)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPut("lead-sources/{id:int}")]
    public async Task<IActionResult> UpdateLeadSource(int id, [FromBody] SaveTravelLeadSourceDto dto)
        => Ok(await Mediator.Send(new SaveTravelLeadSourceCommand(id, dto)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpDelete("lead-sources/{id:int}")]
    public async Task<IActionResult> DeleteLeadSource(int id) => Ok(await Mediator.Send(new DeleteTravelLeadSourceCommand(id)));

    [HttpGet("lost-reasons")]
    public async Task<IActionResult> GetLostReasons([FromQuery] bool? isActive)
        => Ok(await Mediator.Send(new GetTravelLostReasonsQuery(isActive)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPost("lost-reasons")]
    public async Task<IActionResult> CreateLostReason([FromBody] SaveTravelLostReasonDto dto)
        => Created(string.Empty, await Mediator.Send(new SaveTravelLostReasonCommand(null, dto)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPut("lost-reasons/{id:int}")]
    public async Task<IActionResult> UpdateLostReason(int id, [FromBody] SaveTravelLostReasonDto dto)
        => Ok(await Mediator.Send(new SaveTravelLostReasonCommand(id, dto)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpDelete("lost-reasons/{id:int}")]
    public async Task<IActionResult> DeleteLostReason(int id) => Ok(await Mediator.Send(new DeleteTravelLostReasonCommand(id)));

    /// <summary>Tenant TravelNexus settings (defaults when never saved).</summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings() => Ok(await Mediator.Send(new GetTravelSettingsQuery()));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateTravelSettingsDto dto)
        => Ok(await Mediator.Send(new UpdateTravelSettingsCommand(dto)));
}
