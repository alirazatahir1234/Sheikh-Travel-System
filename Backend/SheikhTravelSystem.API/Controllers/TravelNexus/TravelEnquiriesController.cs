using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// CRM enquiries: leads, follow-ups, communication history, conversion and loss.
/// Status moves are validated by the server-side enquiry state machine; concurrent changes return 409.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.EnquiryView)]
[Route("api/travel/enquiries")]
public class TravelEnquiriesController : BaseApiController
{
    /// <summary>Paged list. Filters: search, status, serviceType, agentUserId, branchId, customerId, createdFrom/To (UTC dates), followUpDue.</summary>
    [HttpGet]
    public async Task<IActionResult> GetEnquiries([FromQuery] GetTravelEnquiriesQuery query) => Ok(await Mediator.Send(query));

    /// <summary>Current count per status (zero-filled; Total = sum) and due follow-ups.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary() => Ok(await Mediator.Send(new GetTravelEnquirySummaryQuery()));

    [HttpGet("by-customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId) => Ok(await Mediator.Send(new GetTravelEnquiriesByCustomerQuery(customerId)));

    /// <summary>Enquiry with activity timeline and allowed actions.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetEnquiry(int id) => Ok(await Mediator.Send(new GetTravelEnquiryByIdQuery(id)));

    [RequirePermission(TravelPermissions.EnquiryManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTravelEnquiryDto dto)
        => Created(string.Empty, await Mediator.Send(new CreateTravelEnquiryCommand(dto)));

    [RequirePermission(TravelPermissions.EnquiryManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveTravelEnquiryDto dto)
        => Ok(await Mediator.Send(new UpdateTravelEnquiryCommand(id, dto)));

    /// <summary>MarkContacted | MarkQualified | MarkQuoted | Reopen.</summary>
    [RequirePermission(TravelPermissions.EnquiryManage)]
    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeEnquiryStatusRequest body)
        => Ok(await Mediator.Send(new ChangeTravelEnquiryStatusCommand(id, body.Action, body.Note)));

    [RequirePermission(TravelPermissions.EnquiryManage)]
    [HttpPost("{id:int}/lost")]
    public async Task<IActionResult> MarkLost(int id, [FromBody] MarkTravelEnquiryLostDto dto)
        => Ok(await Mediator.Send(new MarkTravelEnquiryLostCommand(id, dto)));

    [RequirePermission(TravelPermissions.EnquiryManage)]
    [HttpPost("{id:int}/activities")]
    public async Task<IActionResult> LogActivity(int id, [FromBody] LogTravelEnquiryActivityDto dto)
        => Ok(await Mediator.Send(new LogTravelEnquiryActivityCommand(id, dto)));

    /// <summary>Won: link/create the customer and optionally open a Draft visa case, in one transaction.</summary>
    [RequirePermission(TravelPermissions.EnquiryManage)]
    [HttpPost("{id:int}/convert")]
    public async Task<IActionResult> Convert(int id, [FromBody] ConvertTravelEnquiryDto dto)
        => Ok(await Mediator.Send(new ConvertTravelEnquiryCommand(id, dto)));

    public record ChangeEnquiryStatusRequest(string Action, string? Note);
}
