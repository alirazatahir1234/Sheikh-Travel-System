using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// Visa cases: one case (country + visa type) per customer with many applicants. Pipeline moves are validated by
/// the server-side visa case state machine; outcomes are derived from applicant decisions. Concurrent changes return 409.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.VisaView)]
[Route("api/travel/visa-cases")]
public class TravelVisaCasesController : BaseApiController
{
    /// <summary>Paged list. Filters: search, status, countryCode, visaTypeId, agentUserId, customerId, createdFrom/To (UTC dates).</summary>
    [HttpGet]
    public async Task<IActionResult> GetCases([FromQuery] GetTravelVisaCasesQuery query) => Ok(await Mediator.Send(query));

    /// <summary>Current count per pipeline status (zero-filled; Total = sum).</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary() => Ok(await Mediator.Send(new GetTravelVisaCaseSummaryQuery()));

    [HttpGet("by-customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId) => Ok(await Mediator.Send(new GetTravelVisaCasesByCustomerQuery(customerId)));

    /// <summary>Case with applicants (passport masked without Travel.Traveler.ViewPII), history and allowed actions.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCase(int id) => Ok(await Mediator.Send(new GetTravelVisaCaseByIdQuery(id)));

    [RequirePermission(TravelPermissions.VisaManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTravelVisaCaseDto dto)
        => Created(string.Empty, await Mediator.Send(new CreateTravelVisaCaseCommand(dto)));

    [RequirePermission(TravelPermissions.VisaManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTravelVisaCaseDto dto)
        => Ok(await Mediator.Send(new UpdateTravelVisaCaseCommand(id, dto)));

    /// <summary>StartDocuments | VerifyDocuments | ReopenDocuments | StartProcessing | Hold | Resume | Cancel (note required).</summary>
    [RequirePermission(TravelPermissions.VisaManage)]
    [HttpPost("{id:int}/transition")]
    public async Task<IActionResult> Transition(int id, [FromBody] TransitionTravelVisaCaseDto dto)
        => Ok(await Mediator.Send(new TransitionTravelVisaCaseCommand(id, dto)));

    [RequirePermission(TravelPermissions.VisaManage)]
    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, [FromBody] SubmitTravelVisaCaseDto dto)
        => Ok(await Mediator.Send(new SubmitTravelVisaCaseCommand(id, dto)));

    /// <summary>Adds travelers of the case's customer (only before submission).</summary>
    [RequirePermission(TravelPermissions.VisaManage)]
    [HttpPost("{id:int}/applicants")]
    public async Task<IActionResult> AddApplicants(int id, [FromBody] AddTravelVisaApplicantsDto dto)
        => Ok(await Mediator.Send(new AddTravelVisaApplicantsCommand(id, dto)));

    [RequirePermission(TravelPermissions.VisaManage)]
    [HttpDelete("{id:int}/applicants/{applicantId:int}")]
    public async Task<IActionResult> RemoveApplicant(int id, int applicantId)
        => Ok(await Mediator.Send(new RemoveTravelVisaApplicantCommand(id, applicantId)));

    /// <summary>Approved | Rejected (reason required) | Withdrawn. Requires Travel.Visa.Decide.</summary>
    [RequirePermission(TravelPermissions.VisaDecide)]
    [HttpPost("{id:int}/applicants/{applicantId:int}/decision")]
    public async Task<IActionResult> RecordDecision(int id, int applicantId, [FromBody] RecordTravelVisaDecisionDto dto)
        => Ok(await Mediator.Send(new RecordTravelVisaDecisionCommand(id, applicantId, dto)));
}
