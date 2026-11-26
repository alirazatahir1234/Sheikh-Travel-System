using System.Globalization;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Reports.Queries;

/// <summary>
/// One report of the catalogue in the self-describing <see cref="ReportResponseDto"/> shape. <see cref="Group"/> is set by the route
/// (operational / finance / profit), so a report can only be produced through the route whose permission it needs.
/// </summary>
public record GetTravelReportQuery(TravelReportGroup Group, string Type, DateTime? From, DateTime? To, int? BranchId, int? AgentUserId, string? Dimension,
    string? CountryCode) : IRequest<ApiResponse<ReportResponseDto>>;

public class GetTravelReportQueryValidator : AbstractValidator<GetTravelReportQuery>
{
    public GetTravelReportQueryValidator()
    {
        RuleFor(x => x.Type).Must((q, t) => TravelReportCatalog.Find(t) is { } d && d.Group == q.Group).WithMessage("Unknown report.");
        RuleFor(x => x.From).NotNull().When(x => TravelReportCatalog.Find(x.Type)?.Windowed == true).WithMessage("Choose a date range.");
        RuleFor(x => x.To).NotNull().When(x => TravelReportCatalog.Find(x.Type)?.Windowed == true).WithMessage("Choose a date range.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || (x.To.Value - x.From.Value).TotalDays <= 731).WithMessage("The range can be at most two years.");
        RuleFor(x => x.Dimension).Must(d => d is "Agent" or "Branch").When(x => x.Type == "agent-branch-performance" && !string.IsNullOrEmpty(x.Dimension))
            .WithMessage("Dimension is Agent or Branch.");
        RuleFor(x => x.CountryCode).Must(TravelValues.IsCountryCode).When(x => !string.IsNullOrEmpty(x.CountryCode));
    }
}

public class GetTravelReportQueryHandler(ITravelReportRepository reports, ITravelInvoiceRepository invoices, ITravelSupplierBillRepository bills,
    ITravelProfitabilityRepository facts, TravelProfitabilityService profitability, ITravelCommissionRepository commissions,
    ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext) : IRequestHandler<GetTravelReportQuery, ApiResponse<ReportResponseDto>>
{
    private static IReadOnlyDictionary<string, object?> F(params (string, object?)[] v) => TravelReportBuilder.Fields(v);

    public async Task<ApiResponse<ReportResponseDto>> Handle(GetTravelReportQuery request, CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var definition = TravelReportCatalog.Find(request.Type)!;
        var scope = await scopeResolver.ResolveAsync(tenantId, ct);
        var today = DateTime.UtcNow.Date;
        var from = request.From?.Date ?? today;
        var to = request.To?.Date ?? today;
        var b = new TravelReportBuilder(definition);
        // An inverted range is empty by definition — never fall back to unfiltered data.
        var empty = definition.Windowed && to < from;
        var filter = new TravelReportFilter(from, to, request.BranchId, request.AgentUserId, string.IsNullOrEmpty(request.CountryCode) ? null : request.CountryCode);

        switch (definition.Type)
        {
            case "enquiry-performance":
                b.Column("agent", "Agent").Column("branch", "Branch").Column("enquiries", "Enquiries", "number").Column("won", "Won", "number")
                    .Column("lost", "Lost", "number").Column("open", "Open", "number").Column("conversion", "Conversion %", "number").Column("avgDaysToWin", "Avg days to win", "number");
                if (!empty)
                    foreach (var r in await reports.GetEnquiryPerformanceAsync(tenantId, scope, filter, ct))
                        b.Row(r.AgentUserId?.ToString(CultureInfo.InvariantCulture) ?? "none", r.AgentName ?? "Unassigned", r.Enquiries, r.Won, F(("agent", r.AgentName ?? "Unassigned"),
                            ("branch", r.BranchName), ("enquiries", r.Enquiries), ("won", r.Won), ("lost", r.Lost), ("open", r.Open),
                            ("conversion", Pct(r.Won, r.Enquiries)), ("avgDaysToWin", Round1(r.AvgDaysToWin))));
                b.SumColumn("enquiries").SumColumn("won").SumColumn("lost").SumColumn("open");
                break;

            case "visa-processing":
                b.Column("country", "Country").Column("visaType", "Visa type").Column("submitted", "Submitted", "number").Column("approved", "Approved", "number")
                    .Column("rejected", "Rejected", "number").Column("pending", "Pending", "number").Column("approvalRate", "Approval %", "number").Column("avgDays", "Avg processing days", "number");
                if (!empty)
                    foreach (var r in await reports.GetVisaProcessingAsync(tenantId, scope, filter, ct))
                        b.Row($"{r.CountryCode}:{r.VisaTypeName}", $"{r.CountryName} · {r.VisaTypeName}", r.Submitted, r.Approved, F(("country", r.CountryName), ("visaType", r.VisaTypeName),
                            ("submitted", r.Submitted), ("approved", r.Approved), ("rejected", r.Rejected), ("pending", r.Pending),
                            ("approvalRate", Pct(r.Approved, r.Approved + r.Rejected)), ("avgDays", Round1(r.AvgProcessingDays))));
                b.SumColumn("submitted").SumColumn("approved").SumColumn("rejected").SumColumn("pending");
                break;

            case "country-applications":
                b.Column("country", "Country").Column("cases", "Cases", "number").Column("applicants", "Applicants", "number").Column("approved", "Approved", "number")
                    .Column("rejected", "Rejected", "number").Column("pending", "Pending", "number");
                if (!empty)
                    foreach (var r in await reports.GetCountryApplicationsAsync(tenantId, scope, filter, ct))
                        b.Row(r.CountryCode, r.CountryName, r.Cases, r.Applicants, F(("country", r.CountryName), ("cases", r.Cases), ("applicants", r.Applicants),
                            ("approved", r.Approved), ("rejected", r.Rejected), ("pending", r.Pending)));
                b.SumColumn("cases").SumColumn("applicants").SumColumn("approved").SumColumn("rejected").SumColumn("pending");
                break;

            case "pending-documents":
                b.Column("caseNumber", "Case").Column("customer", "Customer").Column("country", "Country").Column("visaType", "Visa type").Column("status", "Status")
                    .Column("travelDate", "Intended travel", "date").Column("outstanding", "Outstanding", "number").Column("agent", "Agent");
                if (!empty)
                    foreach (var r in await reports.GetPendingDocumentsAsync(tenantId, scope, filter, ct))
                        b.Row(r.VisaCaseId.ToString(CultureInfo.InvariantCulture), r.CaseNumber, 1, r.Outstanding, F(("caseNumber", r.CaseNumber), ("customer", r.CustomerName),
                            ("country", r.CountryName), ("visaType", r.VisaTypeName), ("status", r.Status), ("travelDate", r.IntendedTravelDate), ("outstanding", r.Outstanding), ("agent", r.AgentName)));
                b.SumColumn("outstanding");
                break;

            case "group-manifests":
                b.Column("group", "Group").Column("name", "Name").Column("departure", "Departure", "date").Column("return", "Return", "date").Column("status", "Status")
                    .Column("capacity", "Capacity", "number").Column("registered", "Registered", "number").Column("confirmed", "Confirmed", "number")
                    .Column("visasApproved", "Visas approved", "number").Column("ticketed", "Ticketed", "number");
                if (!empty)
                    foreach (var r in await reports.GetGroupManifestsAsync(tenantId, scope, filter, ct))
                        b.Row(r.GroupId.ToString(CultureInfo.InvariantCulture), r.GroupNumber, r.Confirmed, r.Confirmed, F(("group", r.GroupNumber), ("name", r.Name), ("departure", r.DepartureDate),
                            ("return", r.ReturnDate), ("status", r.Status), ("capacity", r.Capacity), ("registered", r.Registered), ("confirmed", r.Confirmed),
                            ("visasApproved", r.VisasApproved), ("ticketed", r.Ticketed)));
                b.SumColumn("capacity").SumColumn("registered").SumColumn("confirmed").SumColumn("visasApproved").SumColumn("ticketed");
                break;

            case "bookings":
                b.Column("service", "Service").Column("supplier", "Supplier").Column("status", "Status").Column("currency", "Currency").Column("services", "Services", "number")
                    .Column("sell", "Sell", "currency").Column("cost", "Cost", "currency").Column("margin", "Margin", "currency");
                if (!empty)
                    foreach (var r in await reports.GetBookingsAsync(tenantId, scope, filter, ct))
                        b.Row($"{r.ServiceType}:{r.SupplierName}:{r.Status}:{r.CurrencyCode}", $"{r.ServiceType} · {r.SupplierName}", r.Services, r.Sell, F(("service", r.ServiceType),
                            ("supplier", r.SupplierName), ("status", r.Status), ("currency", r.CurrencyCode), ("services", r.Services), ("sell", r.Sell), ("cost", r.Cost), ("margin", r.Sell - r.Cost)));
                b.SumColumn("services");
                b.Summary("note", "Sell, cost and margin are per currency row; they are not summed across currencies.");
                break;

            case "agent-branch-performance":
            {
                var byBranch = request.Dimension == "Branch";
                b.Column("label", byBranch ? "Branch" : "Agent").Column("enquiries", "Enquiries", "number").Column("won", "Won", "number").Column("visaCases", "Visa cases", "number")
                    .Column("bookingFiles", "Booking files", "number").Column("currency", "Currency").Column("invoiced", "Invoiced", "currency").Column("collected", "Collected", "currency");
                if (!empty)
                    foreach (var r in await reports.GetPerformanceAsync(tenantId, scope, filter, byBranch, ct))
                        b.Row($"{r.Key}:{r.CurrencyCode}", r.Label, r.Enquiries, r.Invoiced, F(("label", r.Label), ("enquiries", r.Enquiries), ("won", r.Won), ("visaCases", r.VisaCases),
                            ("bookingFiles", r.BookingFiles), ("currency", r.CurrencyCode), ("invoiced", r.Invoiced), ("collected", r.Collected)));
                b.Summary("dimension", byBranch ? "Branch" : "Agent");
                b.Summary("note", "Counts repeat on each currency row of the same agent or branch; invoiced and collected are per currency.");
                break;
            }

            case "customer-balances":
                b.Column("customer", "Customer").Column("currency", "Currency").Column("openInvoices", "Open invoices", "number").Column("invoiced", "Invoiced", "currency")
                    .Column("paid", "Paid", "currency").Column("credited", "Credited", "currency").Column("balance", "Balance", "currency").Column("overdue", "Overdue", "currency")
                    .Column("advances", "Advances", "currency");
                foreach (var r in await invoices.GetCustomerBalancesAsync(tenantId, scope, null, today, true, ct))
                    b.Row($"{r.CustomerId}:{r.CurrencyCode}", r.CustomerName, r.OpenInvoices, r.Balance, F(("customer", r.CustomerName), ("currency", r.CurrencyCode),
                        ("openInvoices", r.OpenInvoices), ("invoiced", r.Invoiced), ("paid", r.Paid), ("credited", r.Credited), ("balance", r.Balance), ("overdue", r.Overdue),
                        ("advances", r.UnallocatedAdvances)));
                b.SumColumn("openInvoices");
                break;

            case "supplier-balances":
                b.Column("supplier", "Supplier").Column("currency", "Currency").Column("openBills", "Open bills", "number").Column("billed", "Billed", "currency")
                    .Column("paid", "Paid", "currency").Column("credited", "Credited", "currency").Column("balance", "Balance", "currency").Column("overdue", "Overdue", "currency")
                    .Column("advances", "Advances", "currency");
                foreach (var r in await bills.GetSupplierBalancesAsync(tenantId, scope, null, today, true, ct))
                    b.Row($"{r.SupplierId}:{r.CurrencyCode}", r.SupplierName, r.OpenBills, r.Balance, F(("supplier", r.SupplierName), ("currency", r.CurrencyCode),
                        ("openBills", r.OpenBills), ("billed", r.Billed), ("paid", r.Paid), ("credited", r.Credited), ("balance", r.Balance), ("overdue", r.Overdue),
                        ("advances", r.UnallocatedAdvances)));
                b.SumColumn("openBills");
                break;

            case "pilgrim-payments":
                b.Column("group", "Group").Column("name", "Name").Column("departure", "Departure", "date").Column("currency", "Currency").Column("pilgrims", "Pilgrims", "number")
                    .Column("quoted", "Quoted", "currency").Column("invoiced", "Invoiced", "currency").Column("paid", "Paid", "currency").Column("balance", "Balance", "currency");
                if (!empty)
                    foreach (var r in await reports.GetPilgrimPaymentsAsync(tenantId, scope, filter, ct))
                        b.Row($"{r.GroupId}:{r.CurrencyCode}", r.GroupNumber, r.Pilgrims, r.Balance, F(("group", r.GroupNumber), ("name", r.GroupName), ("departure", r.DepartureDate),
                            ("currency", r.CurrencyCode), ("pilgrims", r.Pilgrims), ("quoted", r.Quoted), ("invoiced", r.Invoiced), ("paid", r.Paid), ("balance", r.Balance)));
                b.SumColumn("pilgrims");
                break;

            case "commissions":
            {
                b.Column("beneficiary", "Beneficiary").Column("type", "Type").Column("currency", "Currency").Column("accrued", "Accrued", "currency")
                    .Column("payable", "Payable", "currency").Column("paid", "Paid out", "currency").Column("balance", "Balance", "currency");
                var fx = await profitability.FxAsync(tenantId, ct);
                if (fx is null) return ApiResponse<ReportResponseDto>.FailResponse("Set the reporting currency in Travel settings first.");
                if (!empty)
                {
                    var (_, lines) = await profitability.CommissionLinesAsync(tenantId, fx, null, null, from, to, ct);
                    var payouts = (await commissions.GetPayoutsAsync(tenantId, null, null, ct)).Where(p => p.PaidOn.Date >= from && p.PaidOn.Date <= to).ToList();
                    foreach (var r in TravelCommissionEngine.Balances(lines, payouts))
                        b.Row($"{r.BeneficiaryType}:{r.BeneficiaryId}:{r.CurrencyCode}", r.BeneficiaryName, 1, r.Payable, F(("beneficiary", r.BeneficiaryName),
                            ("type", r.BeneficiaryType == "Staff" ? "Staff agent" : "Sub-agent"), ("currency", r.CurrencyCode), ("accrued", r.Accrued), ("payable", r.Payable),
                            ("paid", r.Paid), ("balance", r.Balance)));
                    b.Summary("missingRates", fx.Missing.Count);
                }
                b.Summary("reportingCurrencyCode", fx.ReportingCurrency);
                break;
            }

            case "profit-loss":
            {
                var fx = await profitability.FxAsync(tenantId, ct);
                if (fx is null) return ApiResponse<ReportResponseDto>.FailResponse("Set the reporting currency in Travel settings first.");
                b.Column("month", "Travel month").Column("contexts", "Files / cases / groups", "number").Column("revenue", "Revenue", "currency").Column("cost", "Cost", "currency")
                    .Column("estimatedCost", "of which estimated", "currency").Column("commission", "Commission", "currency").Column("margin", "Margin", "currency").Column("marginPercent", "Margin %", "number");
                if (!empty)
                {
                    var contexts = await facts.GetContextsAsync(tenantId, scope, TravelValues.ContextTypes, from, to, ct);
                    contexts = contexts.Where(c => request.BranchId is null || c.BranchId == request.BranchId).Where(c => request.AgentUserId is null || c.AgentUserId == request.AgentUserId).ToList();
                    if (contexts.Count > 0)
                    {
                        var computation = await profitability.ComputeAsync(tenantId, fx, contexts, ct);
                        var rows = computation.Profit.Contexts.Values.Select(TravelProfitabilityEngine.ToDto).ToList();
                        foreach (var g in rows.GroupBy(r => r.TravelDate is DateTime d ? new DateTime(d.Year, d.Month, 1) : DateTime.MinValue).OrderBy(g => g.Key))
                        {
                            var label = g.Key == DateTime.MinValue ? "No travel date" : g.Key.ToString("MMM yyyy", CultureInfo.InvariantCulture);
                            var sum = TravelProfitabilityEngine.Sum(label, label, g);
                            b.Row(label, label, sum.Contexts, sum.Margin, F(("month", label), ("contexts", sum.Contexts), ("revenue", sum.Revenue), ("cost", sum.Cost),
                                ("estimatedCost", sum.EstimatedCost), ("commission", sum.Commission), ("margin", sum.Margin), ("marginPercent", sum.MarginPercent)));
                        }
                    }
                    b.Summary("missingRates", fx.Missing.Count);
                }
                b.SumColumn("contexts").SumColumn("revenue").SumColumn("cost").SumColumn("estimatedCost").SumColumn("commission").SumColumn("margin");
                b.Summary("reportingCurrencyCode", fx.ReportingCurrency);
                break;
            }
        }

        return ApiResponse<ReportResponseDto>.SuccessResponse(b.Build(definition.Windowed ? from : null, definition.Windowed ? to : null, today));
    }

    private static decimal? Pct(int part, int whole) => whole == 0 ? null : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);
    private static decimal? Round1(decimal? v) => v is null ? null : Math.Round(v.Value, 1, MidpointRounding.AwayFromZero);
}

public record GetTravelReportCatalogQuery : IRequest<ApiResponse<IReadOnlyList<TravelReportDefinition>>>;

public class GetTravelReportCatalogQueryHandler : IRequestHandler<GetTravelReportCatalogQuery, ApiResponse<IReadOnlyList<TravelReportDefinition>>>
{
    public Task<ApiResponse<IReadOnlyList<TravelReportDefinition>>> Handle(GetTravelReportCatalogQuery request, CancellationToken cancellationToken)
        => Task.FromResult(ApiResponse<IReadOnlyList<TravelReportDefinition>>.SuccessResponse(TravelReportCatalog.All));
}
