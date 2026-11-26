using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Reports;
using SheikhTravelSystem.Application.Features.TravelNexus.Reports.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// TravelNexus reports in the shared self-describing <c>ReportResponseDto</c> shape. Three route groups keep authorization by permission:
/// operational reports need Travel.Report.View, balance reports Travel.Finance.View, margin and commission reports Travel.Profitability.View.
/// Windowed reports require from/to; an inverted range returns no rows.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.View)]
[Route("api/travel/reports")]
public class TravelReportsController : BaseApiController
{
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog() => Ok(await Mediator.Send(new GetTravelReportCatalogQuery()));

    /// <summary>enquiry-performance · visa-processing · country-applications · pending-documents · group-manifests · bookings · agent-branch-performance (dimension=Agent|Branch).</summary>
    [RequirePermission(TravelPermissions.ReportView)]
    [HttpGet("{type}")]
    public async Task<IActionResult> GetOperational(string type, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? branchId, [FromQuery] int? agentUserId,
        [FromQuery] string? dimension, [FromQuery] string? countryCode)
        => Ok(await Mediator.Send(new GetTravelReportQuery(TravelReportGroup.Operational, type, from, to, branchId, agentUserId, dimension, countryCode)));

    /// <summary>customer-balances · supplier-balances (as of today) · pilgrim-payments (groups departing in the window).</summary>
    [RequirePermission(TravelPermissions.FinanceView)]
    [HttpGet("finance/{type}")]
    public async Task<IActionResult> GetFinance(string type, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? branchId)
        => Ok(await Mediator.Send(new GetTravelReportQuery(TravelReportGroup.Finance, type, from, to, branchId, null, null, null)));

    /// <summary>profit-loss (travel window) · commissions (issue window).</summary>
    [RequirePermission(TravelPermissions.ProfitabilityView)]
    [HttpGet("profit/{type}")]
    public async Task<IActionResult> GetProfit(string type, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? branchId, [FromQuery] int? agentUserId)
        => Ok(await Mediator.Send(new GetTravelReportQuery(TravelReportGroup.Profit, type, from, to, branchId, agentUserId, null, null)));
}
