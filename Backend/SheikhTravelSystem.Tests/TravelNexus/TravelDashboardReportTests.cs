using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Dashboard.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Dashboard.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Reports;
using SheikhTravelSystem.Application.Features.TravelNexus.Reports.Queries;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelDashboardReportTests
{
    private const int Tenant = 9;
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ITravelBranchScopeResolver> _scope = new();
    private readonly Mock<ITravelReportRepository> _reports = new();
    private readonly Mock<ITravelInvoiceRepository> _invoices = new();
    private readonly Mock<ITravelSupplierBillRepository> _bills = new();
    private readonly Mock<ITravelProfitabilityRepository> _facts = new();
    private readonly Mock<ITravelCommissionRepository> _commissions = new();
    private readonly Mock<ITravelSupplierSetupRepository> _setup = new();
    private readonly Mock<ITravelConfigRepository> _config = new();
    private readonly Mock<ITravelReminderRepository> _reminders = new();

    public TravelDashboardReportTests()
    {
        _tenant.Setup(t => t.GetRequiredTenantId()).Returns(Tenant);
        _scope.Setup(s => s.ResolveAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(TravelBranchScope.CompanyWide);
        _config.Setup(c => c.GetSettingsAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelSettingsDto { ReportingCurrencyCode = "PKR" });
        _setup.Setup(s => s.GetExchangeRatesAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private GetTravelReportQueryHandler ReportHandler() => new(_reports.Object, _invoices.Object, _bills.Object, _facts.Object,
        new TravelProfitabilityService(_facts.Object, _commissions.Object, _setup.Object, _config.Object), _commissions.Object, _scope.Object, _tenant.Object);

    // ── Dashboard rules ──

    [Fact]
    public void Pipelines_are_zero_filled_and_their_parts_sum_to_the_total()
    {
        var p = TravelDashboardRules.Pipeline(new Dictionary<string, int> { ["Won"] = 3, ["New"] = 2 }, TravelValues.EnquiryStatuses);
        p.Counts.Keys.Should().BeEquivalentTo(TravelValues.EnquiryStatuses);
        p.Counts["Lost"].Should().Be(0);
        p.Total.Should().Be(5).And.Be(p.Counts.Values.Sum());
    }

    [Fact]
    public void Trend_periods_are_filled_so_charts_show_every_week_and_month()
    {
        var monday = TravelDashboardRules.WeekStart(new DateTime(2027, 3, 10)); // Wednesday
        monday.Should().Be(new DateTime(2027, 3, 8));
        TravelDashboardRules.WeekStart(new DateTime(2027, 3, 7)).Should().Be(new DateTime(2027, 3, 1), "Sunday belongs to the week starting Monday");
        var weeks = TravelDashboardRules.FillWeeks([new TravelTrendPointDto { PeriodStart = monday, Total = 4, Won = 1 }], monday.AddDays(-14), 3);
        weeks.Select(w => w.Total).Should().Equal(0, 0, 4);
        var months = TravelDashboardRules.FillMonths([], new DateTime(2027, 1, 1), 6);
        months.Should().HaveCount(6);
        months.Last().Label.Should().Be("Jun 2027");
    }

    [Fact]
    public void Upcoming_window_is_validated()
    {
        var v = new GetTravelUpcomingQueryValidator();
        v.TestValidate(new GetTravelUpcomingQuery(0)).ShouldHaveValidationErrorFor(x => x.Days);
        v.TestValidate(new GetTravelUpcomingQuery(91)).ShouldHaveValidationErrorFor(x => x.Days);
        v.TestValidate(new GetTravelUpcomingQuery(14)).ShouldNotHaveAnyValidationErrors();
    }

    // ── Reminder handlers ──

    [Fact]
    public void Rule_save_is_validated_against_the_catalogue()
    {
        var v = new SaveTravelReminderRuleCommandValidator();
        var lead = new TravelReminderLeadDto { LeadDays = 7, StaffInApp = true };
        v.TestValidate(new SaveTravelReminderRuleCommand("Nope", new SaveTravelReminderRuleDto([lead], null))).ShouldHaveValidationErrorFor(x => x.RuleType);
        v.TestValidate(new SaveTravelReminderRuleCommand("InvoiceDue", new SaveTravelReminderRuleDto([lead, new TravelReminderLeadDto { LeadDays = 7 }], null)))
            .ShouldHaveValidationErrorFor(x => x.Rule.Leads);
        v.TestValidate(new SaveTravelReminderRuleCommand("InvoiceOverdue", new SaveTravelReminderRuleDto([lead], null))).ShouldHaveValidationErrorFor(x => x);
        v.TestValidate(new SaveTravelReminderRuleCommand("InvoiceDue", new SaveTravelReminderRuleDto([lead], new string('x', 1001)))).ShouldHaveValidationErrorFor(x => x.Rule.CustomerTemplate);
        v.TestValidate(new SaveTravelReminderRuleCommand("InvoiceDue", new SaveTravelReminderRuleDto([lead], "Pay {{Amount}}"))).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Saving_a_rule_drops_channels_the_rule_does_not_support_and_returns_the_merged_rule()
    {
        SaveTravelReminderRuleDto? saved = null;
        _reminders.Setup(r => r.SaveRuleAsync(Tenant, "EnquiryFollowUp", It.IsAny<SaveTravelReminderRuleDto>(), 7, It.IsAny<CancellationToken>()))
            .Callback<int, string, SaveTravelReminderRuleDto, int?, CancellationToken>((_, _, d, _, _) => saved = d).Returns(Task.CompletedTask);
        _reminders.Setup(r => r.GetRulesAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [new TravelReminderRuleRow("EnquiryFollowUp", 0, true, true, true, false, false, false, null)]);
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(7);

        var result = await new SaveTravelReminderRuleCommandHandler(_reminders.Object, _tenant.Object, user.Object)
            .Handle(new SaveTravelReminderRuleCommand("EnquiryFollowUp", new SaveTravelReminderRuleDto(
                [new TravelReminderLeadDto { LeadDays = 0, StaffInApp = true, StaffEmail = true, CustomerEmail = true, CustomerWhatsApp = true }], " ")), default);

        saved!.Leads.Single().CustomerEmail.Should().BeFalse();
        saved.Leads.Single().CustomerWhatsApp.Should().BeFalse();
        saved.CustomerTemplate.Should().BeNull();
        result.Data!.IsCustomised.Should().BeTrue();
    }

    [Fact]
    public async Task An_inverted_log_range_is_empty_without_querying()
    {
        var result = await new GetTravelReminderLogQueryHandler(_reminders.Object, _tenant.Object)
            .Handle(new GetTravelReminderLogQuery { From = new DateTime(2027, 3, 10), To = new DateTime(2027, 3, 1) }, default);
        result.Data!.Items.Should().BeEmpty();
        _reminders.Verify(r => r.GetLogAsync(It.IsAny<int>(), It.IsAny<TravelReminderLogFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Reports ──

    [Fact]
    public void Report_catalogue_covers_the_brief_and_each_type_belongs_to_one_group()
    {
        TravelReportCatalog.All.Should().HaveCount(12);
        TravelReportCatalog.All.Select(d => d.Type).Should().OnlyHaveUniqueItems();
        TravelReportCatalog.All.Where(d => d.Group == TravelReportGroup.Profit).Select(d => d.Type).Should().BeEquivalentTo(["profit-loss", "commissions"]);
        TravelReportCatalog.All.Where(d => d.Group == TravelReportGroup.Finance).Select(d => d.Type).Should().BeEquivalentTo(["customer-balances", "supplier-balances", "pilgrim-payments"]);
    }

    [Fact]
    public void A_report_is_only_valid_through_its_own_route_group_and_needs_its_window()
    {
        var v = new GetTravelReportQueryValidator();
        var from = new DateTime(2027, 1, 1);
        var to = new DateTime(2027, 3, 31);
        v.TestValidate(new GetTravelReportQuery(TravelReportGroup.Operational, "profit-loss", from, to, null, null, null, null)).ShouldHaveValidationErrorFor(x => x.Type);
        v.TestValidate(new GetTravelReportQuery(TravelReportGroup.Operational, "nope", from, to, null, null, null, null)).ShouldHaveValidationErrorFor(x => x.Type);
        v.TestValidate(new GetTravelReportQuery(TravelReportGroup.Operational, "visa-processing", null, null, null, null, null, null)).ShouldHaveValidationErrorFor(x => x.From);
        v.TestValidate(new GetTravelReportQuery(TravelReportGroup.Finance, "customer-balances", null, null, null, null, null, null)).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(new GetTravelReportQuery(TravelReportGroup.Operational, "agent-branch-performance", from, to, null, null, "Team", null)).ShouldHaveValidationErrorFor(x => x.Dimension);
        v.TestValidate(new GetTravelReportQuery(TravelReportGroup.Profit, "profit-loss", from, to, null, null, null, null)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Builder_refuses_a_row_missing_a_column_and_sums_columns_into_the_summary()
    {
        var b = new TravelReportBuilder(TravelReportCatalog.Find("country-applications")!).Column("country", "Country").Column("cases", "Cases", "number");
        var act = () => b.Row("PK", "Pakistan", 1, 1, TravelReportBuilder.Fields(("country", "Pakistan")));
        act.Should().Throw<InvalidOperationException>().WithMessage("*cases*");
        b.Row("SA", "Saudi Arabia", 3, 3, TravelReportBuilder.Fields(("country", "Saudi Arabia"), ("cases", 3)))
         .Row("AE", "UAE", 2, 2, TravelReportBuilder.Fields(("country", "UAE"), ("cases", 2)))
         .SumColumn("cases");
        var report = b.Build(new DateTime(2027, 1, 1), new DateTime(2027, 3, 31), new DateTime(2027, 4, 1));
        report.Summary["cases"].Should().Be(5m);
        report.Summary["rows"].Should().Be(2);
        report.Summary["windowFrom"].Should().Be("2027-01-01");
        report.TotalValue.Should().Be(5);
    }

    [Fact]
    public async Task Operational_report_rows_carry_every_column_and_totals_equal_the_sum_of_rows()
    {
        _reports.Setup(r => r.GetEnquiryPerformanceAsync(Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<TravelReportFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelEnquiryPerformanceRow(7, "Sana", "Lahore", 10, 4, 3, 3, 5.25m), new TravelEnquiryPerformanceRow(null, null, null, 2, 0, 1, 1, null)]);
        var result = await ReportHandler().Handle(new GetTravelReportQuery(TravelReportGroup.Operational, "enquiry-performance", new DateTime(2027, 1, 1), new DateTime(2027, 3, 31), null, null, null, null), default);
        var report = result.Data!;
        report.Rows.Should().HaveCount(2);
        foreach (var row in report.Rows) row.Fields.Keys.Should().Contain(report.Columns.Select(c => c.Key));
        report.Summary["enquiries"].Should().Be(12m);
        report.Summary["won"].Should().Be(4m);
        report.Rows[0].Fields["conversion"].Should().Be(40m);
        report.Rows[1].Label.Should().Be("Unassigned");
        report.Summary["windowLabel"].Should().Be("created");
    }

    [Fact]
    public async Task An_inverted_range_returns_no_rows_and_never_queries()
    {
        var result = await ReportHandler().Handle(new GetTravelReportQuery(TravelReportGroup.Operational, "bookings", new DateTime(2027, 3, 10), new DateTime(2027, 3, 1), null, null, null, null), default);
        result.Data!.Rows.Should().BeEmpty();
        result.Data.Columns.Should().NotBeEmpty("the ERP still renders the grid header");
        _reports.Verify(r => r.GetBookingsAsync(It.IsAny<int>(), It.IsAny<TravelBranchScope>(), It.IsAny<TravelReportFilter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Balance_reports_are_as_of_today_and_reuse_the_finance_repositories()
    {
        _invoices.Setup(i => i.GetCustomerBalancesAsync(Tenant, It.IsAny<TravelBranchScope>(), null, It.IsAny<DateTime>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelCustomerBalanceDto { CustomerId = 5, CustomerName = "Khan", CurrencyCode = "PKR", OpenInvoices = 2, Invoiced = 100, Paid = 40, Balance = 60, Overdue = 10 }]);
        var result = await ReportHandler().Handle(new GetTravelReportQuery(TravelReportGroup.Finance, "customer-balances", null, null, null, null, null, null), default);
        result.Data!.Rows.Single().Fields["balance"].Should().Be(60m);
        result.Data.Summary["windowFrom"].Should().BeNull();
        result.Data.Summary["windowLabel"].Should().Be("as of today");
    }

    [Fact]
    public async Task Profit_reports_need_a_reporting_currency()
    {
        _config.Setup(c => c.GetSettingsAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelSettingsDto());
        var result = await ReportHandler().Handle(new GetTravelReportQuery(TravelReportGroup.Profit, "profit-loss", new DateTime(2027, 1, 1), new DateTime(2027, 3, 31), null, null, null, null), default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Profit_and_loss_groups_contexts_by_travel_month_and_sums_margins()
    {
        _facts.Setup(f => f.GetContextsAsync(Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Application.Features.TravelNexus.Profitability.DTOs.TravelContextFact("Booking", 1, "BKG-1", new DateTime(2027, 2, 10), null, null, null, null),
                new Application.Features.TravelNexus.Profitability.DTOs.TravelContextFact("Booking", 2, "BKG-2", new DateTime(2027, 2, 20), null, null, null, null)]);
        _facts.Setup(f => f.GetRevenueFactsAsync(Tenant, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new(10, "TINV-1", new DateTime(2027, 2, 1), "PKR", 1000, 0, 1000, "Flight", "Booking", 1, "Flight"),
                new(11, "TINV-2", new DateTime(2027, 2, 1), "PKR", 500, 0, 500, "Hotel", "Booking", 2, "Hotel")]);
        _facts.Setup(f => f.GetInvoiceCreditsAsync(Tenant, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _facts.Setup(f => f.GetCostFactsAsync(Tenant, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new("Booking", 1, "PKR", 600, new DateTime(2027, 2, 1), false, "TSB-1", "Bill", 50)]);
        _facts.Setup(f => f.GetBillCreditsAsync(Tenant, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _commissions.Setup(c => c.GetAttributedInvoicesAsync(Tenant, null, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _commissions.Setup(c => c.GetRulesAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await ReportHandler().Handle(new GetTravelReportQuery(TravelReportGroup.Profit, "profit-loss", new DateTime(2027, 1, 1), new DateTime(2027, 3, 31), null, null, null, null), default);
        var row = result.Data!.Rows.Single();
        row.Label.Should().Be("Feb 2027");
        row.Fields["revenue"].Should().Be(1500m);
        row.Fields["cost"].Should().Be(600m);
        row.Fields["margin"].Should().Be(900m);
        result.Data.Summary["margin"].Should().Be(900m);
        result.Data.Summary["reportingCurrencyCode"].Should().Be("PKR");
    }
}
