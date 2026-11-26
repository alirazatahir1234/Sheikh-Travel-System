using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// Supplier finance: bills (from booking costs + manual lines, supplier commission netted), supplier payments with cross-currency
/// allocations, supplier credits, refunds received, payables reports; supplier rate cards and commission rules; exchange rates to the
/// reporting currency. Every action names its own permission.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.View)]
[Route("api/travel/payables")]
public class TravelSupplierFinanceController : BaseApiController
{
    // ── Bills ──
    /// <summary>Paged bills. Filters: search, status, supplierId, currencyCode, billFrom/To, overdue.</summary>
    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("bills")]
    public async Task<IActionResult> GetBills([FromQuery] GetTravelSupplierBillsQuery query) => Ok(await Mediator.Send(query));

    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("bills/{id:int}")]
    public async Task<IActionResult> GetBill(int id) => Ok(await Mediator.Send(new GetTravelSupplierBillByIdQuery(id)));

    /// <summary>A supplier's unbilled services with suggested commission on billDate; with billId, also that draft's services.</summary>
    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("billable")]
    public async Task<IActionResult> GetBillable([FromQuery] int supplierId, [FromQuery] string? currencyCode, [FromQuery] int? billId, [FromQuery] DateTime? billDate)
        => Ok(await Mediator.Send(new GetTravelSupplierBillableQuery(supplierId, currencyCode, billId, billDate)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("suppliers/{supplierId:int}/open-bills")]
    public async Task<IActionResult> GetOpenBills(int supplierId) => Ok(await Mediator.Send(new GetTravelSupplierOpenBillsQuery(supplierId)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.SupplierBillManage)]
    [HttpPost("bills")]
    public async Task<IActionResult> CreateBill([FromBody] CreateTravelSupplierBillDto dto) => Created(string.Empty, await Mediator.Send(new CreateTravelSupplierBillCommand(dto)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.SupplierBillManage)]
    [HttpPut("bills/{id:int}")]
    public async Task<IActionResult> UpdateBill(int id, [FromBody] UpdateTravelSupplierBillDto dto) => Ok(await Mediator.Send(new UpdateTravelSupplierBillCommand(id, dto)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.SupplierBillManage)]
    [HttpPost("bills/{id:int}/post")]
    public async Task<IActionResult> PostBill(int id, [FromBody] PostTravelSupplierBillDto? dto)
        => Ok(await Mediator.Send(new PostTravelSupplierBillCommand(id, dto ?? new PostTravelSupplierBillDto(null, null))));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.FinanceAdjust)]
    [HttpPost("bills/{id:int}/void")]
    public async Task<IActionResult> VoidBill(int id, [FromBody] VoidTravelInvoiceDto dto) => Ok(await Mediator.Send(new VoidTravelSupplierBillCommand(id, dto.Reason)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.SupplierBillManage)]
    [HttpDelete("bills/{id:int}")]
    public async Task<IActionResult> DeleteBill(int id) => Ok(await Mediator.Send(new DeleteTravelSupplierBillCommand(id)));

    // ── Supplier payments ──
    /// <summary>Paged supplier payments. Filters: search, status, supplierId, paidFrom/To, reconciled.</summary>
    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] GetTravelSupplierPaymentsQuery query) => Ok(await Mediator.Send(query));

    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("payments/{id:int}")]
    public async Task<IActionResult> GetPayment(int id) => Ok(await Mediator.Send(new GetTravelSupplierPaymentByIdQuery(id)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.SupplierPaymentManage)]
    [HttpPost("payments")]
    public async Task<IActionResult> RecordPayment([FromBody] RecordTravelSupplierPaymentDto dto)
        => Created(string.Empty, await Mediator.Send(new RecordTravelSupplierPaymentCommand(dto)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.SupplierPaymentManage)]
    [HttpPost("payments/{id:int}/allocations")]
    public async Task<IActionResult> Allocate(int id, [FromBody] AllocateTravelPaymentDto dto) => Ok(await Mediator.Send(new AllocateTravelSupplierPaymentCommand(id, dto)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.SupplierPaymentManage)]
    [HttpDelete("payments/{id:int}/allocations/{billId:int}")]
    public async Task<IActionResult> Unallocate(int id, int billId) => Ok(await Mediator.Send(new UnallocateTravelSupplierPaymentCommand(id, billId)));

    /// <summary>Completed (cleared) | Failed (bounced / reversed).</summary>
    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.SupplierPaymentManage)]
    [HttpPost("payments/{id:int}/status")]
    public async Task<IActionResult> ChangePaymentStatus(int id, [FromBody] ChangeTravelPaymentStatusDto dto)
        => Ok(await Mediator.Send(new ChangeTravelSupplierPaymentStatusCommand(id, dto)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.FinanceReconcile)]
    [HttpPost("payments/{id:int}/reconcile")]
    public async Task<IActionResult> Reconcile(int id, [FromBody] ReconcileTravelPaymentDto dto) => Ok(await Mediator.Send(new ReconcileTravelSupplierPaymentCommand(id, dto)));

    // ── Supplier credits and refunds received ──
    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("credits")]
    public async Task<IActionResult> GetCredits([FromQuery] GetTravelSupplierCreditsQuery query) => Ok(await Mediator.Send(query));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.FinanceAdjust)]
    [HttpPost("credits")]
    public async Task<IActionResult> IssueCredit([FromBody] IssueTravelSupplierCreditDto dto) => Created(string.Empty, await Mediator.Send(new IssueTravelSupplierCreditCommand(dto)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.FinanceAdjust)]
    [HttpPost("credits/{id:int}/void")]
    public async Task<IActionResult> VoidCredit(int id, [FromBody] VoidTravelCreditNoteDto dto) => Ok(await Mediator.Send(new VoidTravelSupplierCreditCommand(id, dto.Reason)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("refunds")]
    public async Task<IActionResult> GetRefunds([FromQuery] GetTravelSupplierRefundsQuery query) => Ok(await Mediator.Send(query));

    [RequirePermission(TravelPermissions.FinanceView)]
    [RequirePermission(TravelPermissions.FinanceAdjust)]
    [HttpPost("refunds")]
    public async Task<IActionResult> RecordRefund([FromBody] RecordTravelSupplierRefundDto dto) => Created(string.Empty, await Mediator.Send(new RecordTravelSupplierRefundCommand(dto)));

    // ── Reports ──
    /// <summary>Per currency: outstanding and supplier credit due (all time), overdue (as of today), paid this month, unallocated advances.</summary>
    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary() => Ok(await Mediator.Send(new GetTravelPayablesSummaryQuery()));

    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances([FromQuery] int? supplierId, [FromQuery] bool openOnly = true)
        => Ok(await Mediator.Send(new GetTravelSupplierBalancesQuery(supplierId, openOnly)));

    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("suppliers/{supplierId:int}/statement")]
    public async Task<IActionResult> GetStatement(int supplierId, [FromQuery] string? currencyCode, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetTravelSupplierStatementQuery(supplierId, currencyCode, from, to)));

    // ── Exchange rates (to the reporting currency) ──
    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("exchange-rates")]
    public async Task<IActionResult> GetExchangeRates() => Ok(await Mediator.Send(new GetTravelExchangeRatesQuery()));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPost("exchange-rates")]
    public async Task<IActionResult> CreateExchangeRate([FromBody] SaveTravelExchangeRateDto dto)
        => Created(string.Empty, await Mediator.Send(new SaveTravelExchangeRateCommand(null, dto)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpPut("exchange-rates/{id:int}")]
    public async Task<IActionResult> UpdateExchangeRate(int id, [FromBody] SaveTravelExchangeRateDto dto) => Ok(await Mediator.Send(new SaveTravelExchangeRateCommand(id, dto)));

    [RequirePermission(TravelPermissions.Settings)]
    [HttpDelete("exchange-rates/{id:int}")]
    public async Task<IActionResult> DeleteExchangeRate(int id) => Ok(await Mediator.Send(new DeleteTravelExchangeRateCommand(id)));

    // ── Supplier rate cards and commission rules ──
    [RequirePermission(TravelPermissions.SupplierView)]
    [HttpGet("suppliers/{supplierId:int}/rates")]
    public async Task<IActionResult> GetRates(int supplierId) => Ok(await Mediator.Send(new GetTravelSupplierRatesQuery(supplierId)));

    [RequirePermission(TravelPermissions.SupplierManage)]
    [HttpPost("suppliers/{supplierId:int}/rates")]
    public async Task<IActionResult> CreateRate(int supplierId, [FromBody] SaveTravelSupplierRateDto dto)
        => Created(string.Empty, await Mediator.Send(new SaveTravelSupplierRateCommand(supplierId, null, dto)));

    [RequirePermission(TravelPermissions.SupplierManage)]
    [HttpPut("suppliers/{supplierId:int}/rates/{id:int}")]
    public async Task<IActionResult> UpdateRate(int supplierId, int id, [FromBody] SaveTravelSupplierRateDto dto)
        => Ok(await Mediator.Send(new SaveTravelSupplierRateCommand(supplierId, id, dto)));

    [RequirePermission(TravelPermissions.SupplierManage)]
    [HttpDelete("suppliers/{supplierId:int}/rates/{id:int}")]
    public async Task<IActionResult> DeleteRate(int supplierId, int id) => Ok(await Mediator.Send(new DeleteTravelSupplierRateCommand(supplierId, id)));

    /// <summary>Hotel cost from the supplier's rate cards (per night, room type and quantity); nights without a rate are listed, not guessed.</summary>
    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpPost("rates/quote")]
    public async Task<IActionResult> QuoteRate([FromBody] TravelRateQuoteRequestDto dto) => Ok(await Mediator.Send(new QuoteTravelSupplierRateQuery(dto)));

    [RequirePermission(TravelPermissions.SupplierView)]
    [HttpGet("suppliers/{supplierId:int}/commission-rules")]
    public async Task<IActionResult> GetCommissionRules(int supplierId) => Ok(await Mediator.Send(new GetTravelSupplierCommissionRulesQuery(supplierId)));

    [RequirePermission(TravelPermissions.SupplierManage)]
    [HttpPost("suppliers/{supplierId:int}/commission-rules")]
    public async Task<IActionResult> CreateCommissionRule(int supplierId, [FromBody] SaveTravelSupplierCommissionRuleDto dto)
        => Created(string.Empty, await Mediator.Send(new SaveTravelSupplierCommissionRuleCommand(supplierId, null, dto)));

    [RequirePermission(TravelPermissions.SupplierManage)]
    [HttpPut("suppliers/{supplierId:int}/commission-rules/{id:int}")]
    public async Task<IActionResult> UpdateCommissionRule(int supplierId, int id, [FromBody] SaveTravelSupplierCommissionRuleDto dto)
        => Ok(await Mediator.Send(new SaveTravelSupplierCommissionRuleCommand(supplierId, id, dto)));

    [RequirePermission(TravelPermissions.SupplierManage)]
    [HttpDelete("suppliers/{supplierId:int}/commission-rules/{id:int}")]
    public async Task<IActionResult> DeleteCommissionRule(int supplierId, int id)
        => Ok(await Mediator.Send(new DeleteTravelSupplierCommissionRuleCommand(supplierId, id)));
}
