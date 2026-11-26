using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// Travel customer finance: invoices (built from sold items + manual lines), receipts with cross-currency allocations,
/// credit notes, refunds, balances, statements and reconciliation. Money writes re-check every cap under lock; receipts,
/// credit notes and refunds are idempotent on the client's request key.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.FinanceView)]
[Route("api/travel/finance")]
public class TravelFinanceController : BaseApiController
{
    // ── Invoices ──
    /// <summary>Paged invoices. Filters: search, status, paymentStatus, customerId, currencyCode, issueFrom/To, dueFrom/To, overdue.</summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] GetTravelInvoicesQuery query) => Ok(await Mediator.Send(query));

    [HttpGet("invoices/{id:int}")]
    public async Task<IActionResult> GetInvoice(int id) => Ok(await Mediator.Send(new GetTravelInvoiceByIdQuery(id)));

    /// <summary>Uninvoiced sold items of a customer (optionally one currency); with invoiceId, also that draft's items.</summary>
    [HttpGet("billable")]
    public async Task<IActionResult> GetBillable([FromQuery] int customerId, [FromQuery] string? currencyCode, [FromQuery] int? invoiceId)
        => Ok(await Mediator.Send(new GetTravelBillableItemsQuery(customerId, currencyCode, invoiceId)));

    [HttpGet("customers/{customerId:int}/open-invoices")]
    public async Task<IActionResult> GetOpenInvoices(int customerId) => Ok(await Mediator.Send(new GetTravelOpenInvoicesQuery(customerId)));

    [RequirePermission(TravelPermissions.InvoiceManage)]
    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateTravelInvoiceDto dto) => Created(string.Empty, await Mediator.Send(new CreateTravelInvoiceCommand(dto)));

    [RequirePermission(TravelPermissions.InvoiceManage)]
    [HttpPut("invoices/{id:int}")]
    public async Task<IActionResult> UpdateInvoice(int id, [FromBody] UpdateTravelInvoiceDto dto) => Ok(await Mediator.Send(new UpdateTravelInvoiceCommand(id, dto)));

    [RequirePermission(TravelPermissions.InvoiceManage)]
    [HttpPost("invoices/{id:int}/issue")]
    public async Task<IActionResult> IssueInvoice(int id, [FromBody] IssueTravelInvoiceDto? dto)
        => Ok(await Mediator.Send(new IssueTravelInvoiceCommand(id, dto ?? new IssueTravelInvoiceDto(null, null))));

    [RequirePermission(TravelPermissions.FinanceAdjust)]
    [HttpPost("invoices/{id:int}/void")]
    public async Task<IActionResult> VoidInvoice(int id, [FromBody] VoidTravelInvoiceDto dto) => Ok(await Mediator.Send(new VoidTravelInvoiceCommand(id, dto)));

    [RequirePermission(TravelPermissions.InvoiceManage)]
    [HttpDelete("invoices/{id:int}")]
    public async Task<IActionResult> DeleteInvoice(int id) => Ok(await Mediator.Send(new DeleteTravelInvoiceCommand(id)));

    // ── Payments ──
    /// <summary>Paged receipts. Filters: search, status, method, customerId, receivedFrom/To, reconciled.</summary>
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] GetTravelPaymentsQuery query) => Ok(await Mediator.Send(query));

    [HttpGet("payments/{id:int}")]
    public async Task<IActionResult> GetPayment(int id) => Ok(await Mediator.Send(new GetTravelPaymentByIdQuery(id)));

    [RequirePermission(TravelPermissions.PaymentManage)]
    [HttpPost("payments")]
    public async Task<IActionResult> RecordPayment([FromBody] RecordTravelPaymentDto dto) => Created(string.Empty, await Mediator.Send(new RecordTravelPaymentCommand(dto)));

    [RequirePermission(TravelPermissions.PaymentManage)]
    [HttpPost("payments/{id:int}/allocations")]
    public async Task<IActionResult> Allocate(int id, [FromBody] AllocateTravelPaymentDto dto) => Ok(await Mediator.Send(new AllocateTravelPaymentCommand(id, dto)));

    [RequirePermission(TravelPermissions.PaymentManage)]
    [HttpDelete("payments/{id:int}/allocations/{invoiceId:int}")]
    public async Task<IActionResult> Unallocate(int id, int invoiceId) => Ok(await Mediator.Send(new UnallocateTravelPaymentCommand(id, invoiceId)));

    /// <summary>Completed (cleared) | Failed (bounced / reversed).</summary>
    [RequirePermission(TravelPermissions.PaymentManage)]
    [HttpPost("payments/{id:int}/status")]
    public async Task<IActionResult> ChangePaymentStatus(int id, [FromBody] ChangeTravelPaymentStatusDto dto)
        => Ok(await Mediator.Send(new ChangeTravelPaymentStatusCommand(id, dto)));

    [RequirePermission(TravelPermissions.FinanceReconcile)]
    [HttpPost("payments/{id:int}/reconcile")]
    public async Task<IActionResult> Reconcile(int id, [FromBody] ReconcileTravelPaymentDto dto) => Ok(await Mediator.Send(new ReconcileTravelPaymentCommand(id, dto)));

    // ── Credit notes and refunds ──
    [HttpGet("credit-notes")]
    public async Task<IActionResult> GetCreditNotes([FromQuery] GetTravelCreditNotesQuery query) => Ok(await Mediator.Send(query));

    [RequirePermission(TravelPermissions.FinanceAdjust)]
    [HttpPost("credit-notes")]
    public async Task<IActionResult> IssueCreditNote([FromBody] IssueTravelCreditNoteDto dto) => Created(string.Empty, await Mediator.Send(new IssueTravelCreditNoteCommand(dto)));

    [RequirePermission(TravelPermissions.FinanceAdjust)]
    [HttpPost("credit-notes/{id:int}/void")]
    public async Task<IActionResult> VoidCreditNote(int id, [FromBody] VoidTravelCreditNoteDto dto) => Ok(await Mediator.Send(new VoidTravelCreditNoteCommand(id, dto)));

    [HttpGet("refunds")]
    public async Task<IActionResult> GetRefunds([FromQuery] GetTravelRefundsQuery query) => Ok(await Mediator.Send(query));

    [RequirePermission(TravelPermissions.FinanceAdjust)]
    [HttpPost("refunds")]
    public async Task<IActionResult> RecordRefund([FromBody] RecordTravelRefundDto dto) => Created(string.Empty, await Mediator.Send(new RecordTravelRefundCommand(dto)));

    // ── Reports ──
    /// <summary>Per currency: outstanding and credit due (all time), overdue (as of today), collected this month, unallocated advances.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary() => Ok(await Mediator.Send(new GetTravelFinanceSummaryQuery()));

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances([FromQuery] int? customerId, [FromQuery] bool openOnly = true)
        => Ok(await Mediator.Send(new GetTravelCustomerBalancesQuery(customerId, openOnly)));

    [HttpGet("customers/{customerId:int}/statement")]
    public async Task<IActionResult> GetStatement(int customerId, [FromQuery] string? currencyCode, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetTravelCustomerStatementQuery(customerId, currencyCode, from, to)));

    [HttpGet("reconciliation/uninvoiced")]
    public async Task<IActionResult> GetUninvoiced() => Ok(await Mediator.Send(new GetTravelUninvoicedQuery()));

    [HttpGet("reconciliation/changed-lines")]
    public async Task<IActionResult> GetChangedLines() => Ok(await Mediator.Send(new GetTravelChangedInvoiceLinesQuery()));

    [HttpGet("groups/{groupId:int}")]
    public async Task<IActionResult> GetGroupFinance(int groupId) => Ok(await Mediator.Send(new GetTravelGroupFinanceQuery(groupId)));
}
