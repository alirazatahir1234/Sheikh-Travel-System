using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// Agent commission for staff and sub-agents: sub-agents, rules, derived commission lines (accrued until the invoice is settled,
/// then payable), balances, payouts and invoice attribution.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.View)]
[Route("api/travel/commissions")]
public class TravelCommissionsController : BaseApiController
{
    // ── Sub-agents (the list feeds the customer "referred by" and invoice pickers) ──
    [HttpGet("sub-agents")]
    public async Task<IActionResult> GetSubAgents([FromQuery] bool? isActive) => Ok(await Mediator.Send(new GetTravelSubAgentsQuery(isActive)));

    [RequirePermission(TravelPermissions.CommissionManage)]
    [HttpPost("sub-agents")]
    public async Task<IActionResult> CreateSubAgent([FromBody] SaveTravelSubAgentDto dto) => Created(string.Empty, await Mediator.Send(new SaveTravelSubAgentCommand(null, dto)));

    [RequirePermission(TravelPermissions.CommissionManage)]
    [HttpPut("sub-agents/{id:int}")]
    public async Task<IActionResult> UpdateSubAgent(int id, [FromBody] SaveTravelSubAgentDto dto) => Ok(await Mediator.Send(new SaveTravelSubAgentCommand(id, dto)));

    [RequirePermission(TravelPermissions.CommissionManage)]
    [HttpDelete("sub-agents/{id:int}")]
    public async Task<IActionResult> DeleteSubAgent(int id) => Ok(await Mediator.Send(new DeleteTravelSubAgentCommand(id)));

    // ── Rules ──
    [RequirePermission(TravelPermissions.CommissionView)]
    [HttpGet("rules")]
    public async Task<IActionResult> GetRules() => Ok(await Mediator.Send(new GetTravelCommissionRulesQuery()));

    [RequirePermission(TravelPermissions.CommissionManage)]
    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] SaveTravelCommissionRuleDto dto) => Created(string.Empty, await Mediator.Send(new SaveTravelCommissionRuleCommand(null, dto)));

    [RequirePermission(TravelPermissions.CommissionManage)]
    [HttpPut("rules/{id:int}")]
    public async Task<IActionResult> UpdateRule(int id, [FromBody] SaveTravelCommissionRuleDto dto) => Ok(await Mediator.Send(new SaveTravelCommissionRuleCommand(id, dto)));

    [RequirePermission(TravelPermissions.CommissionManage)]
    [HttpDelete("rules/{id:int}")]
    public async Task<IActionResult> DeleteRule(int id) => Ok(await Mediator.Send(new DeleteTravelCommissionRuleCommand(id)));

    // ── Commission, balances, payouts ──
    /// <summary>Commission per invoice and beneficiary. Filters: beneficiaryType + beneficiaryId, status (Accrued | Payable), issuedFrom/To.</summary>
    [RequirePermission(TravelPermissions.CommissionView)]
    [HttpGet("lines")]
    public async Task<IActionResult> GetLines([FromQuery] string? beneficiaryType, [FromQuery] int? beneficiaryId, [FromQuery] string? status,
        [FromQuery] DateTime? issuedFrom, [FromQuery] DateTime? issuedTo)
        => Ok(await Mediator.Send(new GetTravelCommissionLinesQuery(beneficiaryType, beneficiaryId, status, issuedFrom, issuedTo)));

    [RequirePermission(TravelPermissions.CommissionView)]
    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances([FromQuery] string? beneficiaryType, [FromQuery] int? beneficiaryId)
        => Ok(await Mediator.Send(new GetTravelCommissionBalancesQuery(beneficiaryType, beneficiaryId)));

    [RequirePermission(TravelPermissions.CommissionView)]
    [HttpGet("payouts")]
    public async Task<IActionResult> GetPayouts([FromQuery] string? beneficiaryType, [FromQuery] int? beneficiaryId)
        => Ok(await Mediator.Send(new GetTravelCommissionPayoutsQuery(beneficiaryType, beneficiaryId)));

    [RequirePermission(TravelPermissions.CommissionManage)]
    [HttpPost("payouts")]
    public async Task<IActionResult> RecordPayout([FromBody] RecordTravelCommissionPayoutDto dto)
        => Created(string.Empty, await Mediator.Send(new RecordTravelCommissionPayoutCommand(dto)));

    /// <summary>Re-assigns an invoice's commission to a staff agent and/or sub-agent (null clears); history is kept.</summary>
    [RequirePermission(TravelPermissions.CommissionManage)]
    [HttpPut("invoices/{invoiceId:int}/attribution")]
    public async Task<IActionResult> UpdateAttribution(int invoiceId, [FromBody] UpdateTravelInvoiceAttributionDto dto)
        => Ok(await Mediator.Send(new UpdateTravelInvoiceAttributionCommand(invoiceId, dto)));
}
