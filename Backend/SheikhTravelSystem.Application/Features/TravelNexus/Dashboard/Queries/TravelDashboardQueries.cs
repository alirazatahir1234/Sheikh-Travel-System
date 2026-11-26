using System.Globalization;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Dashboard.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Dashboard.Queries;

/// <summary>Pure dashboard arithmetic: zero-filled pipelines, period filling for trends.</summary>
public static class TravelDashboardRules
{
    public const int EnquiryWeeks = 8;
    public const int TrendMonths = 6;

    public static TravelPipelineDto Pipeline(IReadOnlyDictionary<string, int> counts, string[] statuses)
    {
        var filled = statuses.ToDictionary(s => s, s => counts.TryGetValue(s, out var n) ? n : 0);
        return new TravelPipelineDto { Counts = filled, Total = filled.Values.Sum() };
    }

    /// <summary>Monday of the ISO week containing <paramref name="utc"/>.</summary>
    public static DateTime WeekStart(DateTime utc)
    {
        var d = utc.Date;
        var offset = ((int)d.DayOfWeek + 6) % 7;
        return d.AddDays(-offset);
    }

    /// <summary>Every week from <paramref name="firstWeek"/> for <paramref name="weeks"/> weeks, zero where nothing happened.</summary>
    public static IReadOnlyList<TravelTrendPointDto> FillWeeks(IReadOnlyList<TravelTrendPointDto> rows, DateTime firstWeek, int weeks)
        => Enumerable.Range(0, weeks).Select(i => firstWeek.AddDays(7 * i)).Select(w =>
            rows.FirstOrDefault(r => r.PeriodStart.Date == w) ?? new TravelTrendPointDto { PeriodStart = w, Label = w.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }).ToList();

    public static IReadOnlyList<TravelDecisionTrendPointDto> FillMonths(IReadOnlyList<TravelDecisionTrendPointDto> rows, DateTime firstMonth, int months)
        => Enumerable.Range(0, months).Select(i => firstMonth.AddMonths(i)).Select(m =>
            rows.FirstOrDefault(r => r.PeriodStart.Year == m.Year && r.PeriodStart.Month == m.Month)
            ?? new TravelDecisionTrendPointDto { PeriodStart = m, Label = m.ToString("MMM yyyy", CultureInfo.InvariantCulture) }).ToList();
}

public record GetTravelDashboardQuery : IRequest<ApiResponse<TravelDashboardDto>>;

public class GetTravelDashboardQueryHandler(ITravelEnquiryRepository enquiries, ITravelVisaCaseRepository visaCases, ITravelGroupRepository groups,
    ITravelBookingRepository bookings, ITravelDocumentRepository documents, ITravelInvoiceRepository invoices, ITravelSupplierBillRepository bills,
    ITravelReminderRepository reminders, ITravelDashboardRepository dashboard, ITravelConfigRepository config, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext) : IRequestHandler<GetTravelDashboardQuery, ApiResponse<TravelDashboardDto>>
{
    public async Task<ApiResponse<TravelDashboardDto>> Handle(GetTravelDashboardQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var now = DateTime.UtcNow;
        var today = now.Date;
        var settings = await config.GetSettingsAsync(tenantId, cancellationToken);

        var (enquiryCounts, followUpsDue) = await enquiries.GetSummaryAsync(tenantId, scope, now, cancellationToken);
        var dto = new TravelDashboardDto
        {
            AsOf = now,
            Enquiries = TravelDashboardRules.Pipeline(enquiryCounts, TravelValues.EnquiryStatuses),
            VisaCases = TravelDashboardRules.Pipeline(await visaCases.GetStatusCountsAsync(tenantId, scope, cancellationToken), TravelValues.VisaCaseStatuses),
            Groups = TravelDashboardRules.Pipeline(await groups.GetStatusCountsAsync(tenantId, scope, cancellationToken), TravelValues.GroupStatuses),
            Bookings = TravelDashboardRules.Pipeline(await bookings.GetStatusCountsAsync(tenantId, scope, cancellationToken), TravelValues.BookingStatuses),
            Receivables = await invoices.GetCurrencySummaryAsync(tenantId, scope, today, new DateTime(today.Year, today.Month, 1), cancellationToken),
            Payables = await bills.GetCurrencySummaryAsync(tenantId, scope, today, new DateTime(today.Year, today.Month, 1), cancellationToken)
        };

        // Attention counts come from the same candidate queries the reminders use.
        var (overdueCount, overdueAmounts) = await dashboard.GetOverdueInvoicesAsync(tenantId, scope, today, cancellationToken);
        var weekAgo = now.AddDays(-7);
        var reminderCounts = await reminders.GetStatusCountsAsync(tenantId, weekAgo, cancellationToken);
        dto.Attention = new TravelAttentionDto
        {
            FollowUpsDue = followUpsDue,
            MissingDocumentCases = (await documents.GetMissingAsync(tenantId, scope, 1, 1, cancellationToken)).TotalCount,
            TicketingDueNext72Hours = await bookings.CountTicketingDueAsync(tenantId, scope, now.AddHours(72), cancellationToken),
            PassportWarningDays = settings.PassportExpiryWarningDays,
            PassportsExpiring = (await reminders.GetCandidatesAsync(tenantId, "PassportExpiry", today, today.AddDays(settings.PassportExpiryWarningDays), scope, cancellationToken)).Count,
            AppointmentsNext7Days = (await reminders.GetCandidatesAsync(tenantId, "VisaAppointment", now, now.AddDays(7), scope, cancellationToken)).Count,
            DeparturesNext7Days = (await reminders.GetCandidatesAsync(tenantId, "FlightDeparture", now, now.AddDays(7), scope, cancellationToken)).Count
                                  + (await reminders.GetCandidatesAsync(tenantId, "GroupDeparture", today, today.AddDays(7), scope, cancellationToken)).Count,
            OverdueInvoices = overdueCount,
            OverdueInvoiceAmounts = overdueAmounts,
            OverdueSupplierBills = await dashboard.CountOverdueSupplierBillsAsync(tenantId, scope, today, cancellationToken),
            RemindersFailedLast7Days = reminderCounts.GetValueOrDefault("Failed"),
            RemindersSkippedNoAgentLast7Days = await reminders.CountSkippedNoAgentAsync(tenantId, weekAgo, cancellationToken)
        };

        var firstWeek = TravelDashboardRules.WeekStart(today).AddDays(-7 * (TravelDashboardRules.EnquiryWeeks - 1));
        var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-(TravelDashboardRules.TrendMonths - 1));
        dto.Trends = new TravelTrendsDto
        {
            EnquiryWeeks = TravelDashboardRules.EnquiryWeeks, Months = TravelDashboardRules.TrendMonths,
            Enquiries = TravelDashboardRules.FillWeeks(await dashboard.GetEnquiryTrendAsync(tenantId, scope, firstWeek, cancellationToken), firstWeek, TravelDashboardRules.EnquiryWeeks),
            Invoicing = await dashboard.GetInvoicingTrendAsync(tenantId, scope, firstMonth, cancellationToken),
            VisaDecisions = TravelDashboardRules.FillMonths(await dashboard.GetVisaDecisionTrendAsync(tenantId, scope, firstMonth, cancellationToken), firstMonth, TravelDashboardRules.TrendMonths)
        };
        return ApiResponse<TravelDashboardDto>.SuccessResponse(dto);
    }
}

/// <summary>Deadlines in the next <c>Days</c> days — the reminder candidate queries, so the dashboard and the reminders never disagree.</summary>
public record GetTravelUpcomingQuery(int Days = 14) : IRequest<ApiResponse<TravelUpcomingDto>>;

public class GetTravelUpcomingQueryValidator : AbstractValidator<GetTravelUpcomingQuery>
{
    public GetTravelUpcomingQueryValidator() => RuleFor(x => x.Days).InclusiveBetween(1, 90);
}

public class GetTravelUpcomingQueryHandler(ITravelReminderRepository reminders, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelUpcomingQuery, ApiResponse<TravelUpcomingDto>>
{
    public async Task<ApiResponse<TravelUpcomingDto>> Handle(GetTravelUpcomingQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var now = DateTime.UtcNow;
        var to = now.Date.AddDays(request.Days + 1).AddTicks(-1);
        var items = new List<TravelUpcomingItemDto>();
        foreach (var d in TravelReminderCatalog.All.Where(d => d.Kind == TravelReminderKind.Upcoming))
        {
            var candidates = await reminders.GetCandidatesAsync(tenantId, d.RuleType, now, to, scope, cancellationToken);
            items.AddRange(candidates.Select(c => new TravelUpcomingItemDto
            {
                RuleType = d.RuleType, EntityType = c.EntityType, EntityId = c.EntityId, Label = c.ContextLabel, Reference = c.Reference, DueAt = c.DueAt,
                StaffUserId = c.StaffUserId, CustomerName = c.CustomerName, Amount = c.Amount, CurrencyCode = c.CurrencyCode
            }));
        }
        return ApiResponse<TravelUpcomingDto>.SuccessResponse(new TravelUpcomingDto { Days = request.Days, Items = items.OrderBy(i => i.DueAt).ThenBy(i => i.Label).ToList() });
    }
}

public record GetTravelDashboardProfitQuery : IRequest<ApiResponse<TravelDashboardProfitDto>>;

public class GetTravelDashboardProfitQueryHandler(ITravelProfitabilityRepository facts, TravelProfitabilityService service, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext) : IRequestHandler<GetTravelDashboardProfitQuery, ApiResponse<TravelDashboardProfitDto>>
{
    public async Task<ApiResponse<TravelDashboardProfitDto>> Handle(GetTravelDashboardProfitQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var fx = await service.FxAsync(tenantId, cancellationToken);
        if (fx is null) return ApiResponse<TravelDashboardProfitDto>.FailResponse("Set the reporting currency in Travel settings first.");
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var today = DateTime.UtcNow.Date;
        var thisMonth = new DateTime(today.Year, today.Month, 1);
        var contexts = await facts.GetContextsAsync(tenantId, scope, TravelValues.ContextTypes, thisMonth.AddMonths(-1), thisMonth.AddMonths(1).AddDays(-1), cancellationToken);
        var dto = new TravelDashboardProfitDto { ReportingCurrencyCode = fx.ReportingCurrency };
        if (contexts.Count > 0)
        {
            var computation = await service.ComputeAsync(tenantId, fx, contexts, cancellationToken);
            var rows = computation.Profit.Contexts.Values.Select(TravelProfitabilityEngine.ToDto).ToList();
            dto.ThisMonth = TravelProfitabilityEngine.Sum("this", "This month", rows.Where(r => r.TravelDate >= thisMonth));
            dto.LastMonth = TravelProfitabilityEngine.Sum("last", "Last month", rows.Where(r => r.TravelDate < thisMonth));
            dto.MissingRates = fx.Missing.Select(m => new Profitability.DTOs.TravelMissingRateDto { CurrencyCode = m.Currency, EarliestDate = m.Earliest, Amounts = m.Count }).ToList();
        }
        return ApiResponse<TravelDashboardProfitDto>.SuccessResponse(dto);
    }
}
