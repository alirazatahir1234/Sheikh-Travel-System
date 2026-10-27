using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Invoices.Commands;
using SheikhTravelSystem.Application.Features.Invoices.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(FinancePermissions.InvoiceView)]
public class InvoicesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetInvoicesQuery query)
        => Ok(await Mediator.Send(query));

    [HttpGet("billing-summary")]
    public async Task<IActionResult> GetBillingSummary()
        => Ok(await Mediator.Send(new GetInvoiceBillingSummaryQuery()));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetInvoiceByIdQuery(id)));

    [RequirePermission(FinancePermissions.InvoiceManage)]
    [HttpPost("from-booking/{bookingId:int}")]
    public async Task<IActionResult> IssueFromBooking(int bookingId)
    {
        var result = await Mediator.Send(new IssueFromBookingCommand(bookingId));
        return Created(string.Empty, result);
    }

    [RequirePermission(FinancePermissions.InvoiceManage)]
    [HttpPost("from-trip/{tripId:int}")]
    public async Task<IActionResult> IssueFromTrip(int tripId)
    {
        var result = await Mediator.Send(new IssueFromTripCommand(tripId));
        return Created(string.Empty, result);
    }

    [RequirePermission(FinancePermissions.InvoiceManage)]
    [HttpPost("{id:int}/email")]
    public async Task<IActionResult> EmailToCustomer(int id)
        => Ok(await Mediator.Send(new SendInvoiceEmailCommand(id)));

    [RequirePermission(FinancePermissions.InvoiceManage)]
    [HttpPost("{id:int}/void")]
    public async Task<IActionResult> Void(int id)
        => Ok(await Mediator.Send(new VoidInvoiceCommand(id)));

    [RequirePermission(FinancePermissions.InvoiceManage)]
    [HttpPost("{id:int}/mark-paid")]
    public async Task<IActionResult> MarkPaid(int id)
        => Ok(await Mediator.Send(new MarkInvoicePaidCommand(id)));
}
