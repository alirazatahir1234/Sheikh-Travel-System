using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// Country / visa-type document checklists. Versioned: only Drafts are editable; activating retires the previous
/// Active version, and cases keep the version they were built from.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.View)]
[Route("api/travel/checklists")]
public class TravelChecklistsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetChecklists([FromQuery] string? countryCode, [FromQuery] string? status)
        => Ok(await Mediator.Send(new GetTravelChecklistsQuery(countryCode, status)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetChecklist(int id) => Ok(await Mediator.Send(new GetTravelChecklistByIdQuery(id)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTravelChecklistTemplateDto dto)
        => Created(string.Empty, await Mediator.Send(new CreateTravelChecklistCommand(dto)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveTravelChecklistTemplateDto dto)
        => Ok(await Mediator.Send(new UpdateTravelChecklistCommand(id, dto)));

    /// <summary>Replaces all items of a Draft checklist (order = array order).</summary>
    [RequirePermission(TravelPermissions.Settings)]
    [HttpPut("{id:int}/items")]
    public async Task<IActionResult> SaveItems(int id, [FromBody] IReadOnlyList<SaveTravelChecklistItemDto> items)
        => Ok(await Mediator.Send(new SaveTravelChecklistItemsCommand(id, items)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id) => Ok(await Mediator.Send(new ActivateTravelChecklistCommand(id)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPost("{id:int}/new-version")]
    public async Task<IActionResult> NewVersion(int id) => Ok(await Mediator.Send(new NewTravelChecklistVersionCommand(id)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) => Ok(await Mediator.Send(new DeleteTravelChecklistCommand(id)));
}
