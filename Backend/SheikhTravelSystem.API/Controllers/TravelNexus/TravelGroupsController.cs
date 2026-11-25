using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// Umrah groups: pilgrims, hotel stays, rooming, transport and manifest. Status moves are validated by the group state
/// machine; "StartVisaProcessing" opens one group visa case with all confirmed pilgrims. Concurrent changes return 409.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.GroupView)]
[Route("api/travel/groups")]
public class TravelGroupsController : BaseApiController
{
    /// <summary>Paged list. Filters: search, status, packageId, departureFrom/To (dates, inclusive).</summary>
    [HttpGet]
    public async Task<IActionResult> GetGroups([FromQuery] GetTravelGroupsQuery query) => Ok(await Mediator.Send(query));

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary() => Ok(await Mediator.Send(new GetTravelGroupSummaryQuery()));

    [HttpGet("by-customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId) => Ok(await Mediator.Send(new GetTravelGroupsByCustomerQuery(customerId)));

    /// <summary>Group with pilgrims (passport masked without ViewPII), stays, rooms, transport, history, readiness and allowed actions.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetGroup(int id) => Ok(await Mediator.Send(new GetTravelGroupByIdQuery(id)));

    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTravelGroupDto dto) => Created(string.Empty, await Mediator.Send(new CreateTravelGroupCommand(dto)));

    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTravelGroupDto dto) => Ok(await Mediator.Send(new UpdateTravelGroupCommand(id, dto)));

    /// <summary>Open | CloseRegistration | Reopen | StartVisaProcessing | MarkReady | Unready | Depart | Return | Close | Cancel (note required).</summary>
    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPost("{id:int}/transition")]
    public async Task<IActionResult> Transition(int id, [FromBody] TransitionTravelGroupDto dto) => Ok(await Mediator.Send(new TransitionTravelGroupCommand(id, dto)));

    // ── Pilgrims ──
    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPost("{id:int}/members")]
    public async Task<IActionResult> Register(int id, [FromBody] RegisterTravelGroupMembersDto dto)
        => Ok(await Mediator.Send(new RegisterTravelGroupMembersCommand(id, dto)));

    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPost("{id:int}/members/{memberId:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, int memberId) => Ok(await Mediator.Send(new ConfirmTravelGroupMemberCommand(id, memberId)));

    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPut("{id:int}/members/{memberId:int}")]
    public async Task<IActionResult> UpdateMember(int id, int memberId, [FromBody] UpdateTravelGroupMemberDto dto)
        => Ok(await Mediator.Send(new UpdateTravelGroupMemberCommand(id, memberId, dto)));

    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPost("{id:int}/members/{memberId:int}/cancel")]
    public async Task<IActionResult> CancelMember(int id, int memberId, [FromBody] CancelTravelGroupMemberDto dto)
        => Ok(await Mediator.Send(new CancelTravelGroupMemberCommand(id, memberId, dto)));

    // ── Stays & rooming ──
    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPost("{id:int}/stays")]
    public async Task<IActionResult> AddStay(int id, [FromBody] SaveTravelGroupStayDto dto) => Ok(await Mediator.Send(new SaveTravelGroupStayCommand(id, null, dto)));

    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPut("{id:int}/stays/{stayId:int}")]
    public async Task<IActionResult> UpdateStay(int id, int stayId, [FromBody] SaveTravelGroupStayDto dto)
        => Ok(await Mediator.Send(new SaveTravelGroupStayCommand(id, stayId, dto)));

    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpDelete("{id:int}/stays/{stayId:int}")]
    public async Task<IActionResult> DeleteStay(int id, int stayId) => Ok(await Mediator.Send(new DeleteTravelGroupStayCommand(id, stayId)));

    /// <summary>Suggested rooming for a stay (families together, others by gender) — preview only.</summary>
    [HttpGet("{id:int}/stays/{stayId:int}/rooming/suggest")]
    public async Task<IActionResult> SuggestRooming(int id, int stayId) => Ok(await Mediator.Send(new SuggestTravelGroupRoomingQuery(id, stayId)));

    /// <summary>Replaces rooms and assignments of a stay (validated: capacity, gender/family, one bed per pilgrim).</summary>
    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPut("{id:int}/stays/{stayId:int}/rooming")]
    public async Task<IActionResult> SaveRooming(int id, int stayId, [FromBody] SaveTravelStayRoomingDto dto)
        => Ok(await Mediator.Send(new SaveTravelGroupRoomingCommand(id, stayId, dto)));

    // ── Transport ──
    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPost("{id:int}/transport")]
    public async Task<IActionResult> AddTransport(int id, [FromBody] SaveTravelTransportLegDto dto)
        => Ok(await Mediator.Send(new SaveTravelGroupTransportCommand(id, null, dto)));

    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpPut("{id:int}/transport/{legId:int}")]
    public async Task<IActionResult> UpdateTransport(int id, int legId, [FromBody] SaveTravelTransportLegDto dto)
        => Ok(await Mediator.Send(new SaveTravelGroupTransportCommand(id, legId, dto)));

    [RequirePermission(TravelPermissions.GroupManage)]
    [HttpDelete("{id:int}/transport/{legId:int}")]
    public async Task<IActionResult> DeleteTransport(int id, int legId) => Ok(await Mediator.Send(new DeleteTravelGroupTransportCommand(id, legId)));

    // ── Manifest ──
    /// <summary>Pilgrim manifest data (PDF/Excel are built in the ERP). Audited fail-closed; passports masked without ViewPII.</summary>
    [RequirePermission(TravelPermissions.GroupManifest)]
    [HttpGet("{id:int}/manifest")]
    public async Task<IActionResult> Manifest(int id)
    {
        Response.Headers.CacheControl = "no-store, private";
        return Ok(await Mediator.Send(new GetTravelGroupManifestQuery(id)));
    }
}
