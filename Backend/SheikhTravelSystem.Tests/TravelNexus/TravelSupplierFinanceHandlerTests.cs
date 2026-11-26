using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.DTOs;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelSupplierFinanceHandlerTests
{
    private const int Tenant = 9;
    private const int SupplierId = 4;
    private const int BillId = 61;
    private const int PaymentId = 71;

    private readonly Mock<ITravelSupplierBillRepository> _bills = new();
    private readonly Mock<ITravelSupplierPaymentRepository> _payments = new();
    private readonly Mock<ITravelSupplierRepository> _suppliers = new();
    private readonly Mock<ITravelSupplierSetupRepository> _setup = new();
    private readonly Mock<ITravelCommissionRepository> _commissions = new();
    private readonly Mock<ITravelProfitabilityRepository> _facts = new();
    private readonly Mock<ITravelInvoiceRepository> _invoices = new();
    private readonly Mock<ITravelConfigRepository> _config = new();
    private readonly Mock<ITravelBranchScopeResolver> _scope = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ICurrentUserService> _user = new();

    public TravelSupplierFinanceHandlerTests()
    {
        _tenant.Setup(t => t.GetRequiredTenantId()).Returns(Tenant);
        _user.Setup(u => u.UserId).Returns(7);
        _scope.Setup(s => s.ResolveAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(TravelBranchScope.CompanyWide);
        _suppliers.Setup(s => s.GetByIdAsync(SupplierId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelSupplierDto { Id = SupplierId, Name = "Al Haramain" });
        _config.Setup(c => c.GetSettingsAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelSettingsDto { ReportingCurrencyCode = "PKR", DefaultPaymentTermsDays = 7 });
        _setup.Setup(s => s.GetExchangeRatesAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private TravelSupplierFinanceGuard Guard => new(_bills.Object, _payments.Object, _scope.Object);
    private TravelProfitabilityService Service => new(_facts.Object, _commissions.Object, _setup.Object, _config.Object);

    private void Bill(TravelSupplierBillState state)
        => _bills.Setup(b => b.GetStateAsync(BillId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(state);

    private static TravelSupplierBillState Posted(decimal net = 7600, decimal paid = 0, decimal credited = 0)
        => new() { Id = BillId, Status = "Posted", SupplierId = SupplierId, CurrencyCode = "SAR", Total = net, Paid = paid, Credited = credited, AllocationCount = paid > 0 ? 1 : 0 };

    // ── Another tenant's (or branch's) records are not found ──

    [Fact]
    public async Task Every_bill_and_payment_command_is_not_found_for_another_tenants_id()
    {
        // The guard's GetStateAsync filters on TenantId: another tenant's id comes back null.
        var calls = new Func<Task>[]
        {
            () => new UpdateTravelSupplierBillCommandHandler(_bills.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new UpdateTravelSupplierBillCommand(BillId, new UpdateTravelSupplierBillDto(null, null, null, null, [])), default),
            () => new PostTravelSupplierBillCommandHandler(_bills.Object, _config.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new PostTravelSupplierBillCommand(BillId, new PostTravelSupplierBillDto(null, null)), default),
            () => new VoidTravelSupplierBillCommandHandler(_bills.Object, Guard, _tenant.Object, _user.Object).Handle(new VoidTravelSupplierBillCommand(BillId, "x"), default),
            () => new DeleteTravelSupplierBillCommandHandler(_bills.Object, Guard, _tenant.Object).Handle(new DeleteTravelSupplierBillCommand(BillId), default),
            () => new GetTravelSupplierBillByIdQueryHandler(_bills.Object, Guard, _tenant.Object).Handle(new GetTravelSupplierBillByIdQuery(BillId), default),
            () => new AllocateTravelSupplierPaymentCommandHandler(_payments.Object, _bills.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new AllocateTravelSupplierPaymentCommand(PaymentId, new AllocateTravelPaymentDto([new TravelAllocationInputDto(BillId, 10, null)])), default),
            () => new UnallocateTravelSupplierPaymentCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new UnallocateTravelSupplierPaymentCommand(PaymentId, BillId), default),
            () => new ChangeTravelSupplierPaymentStatusCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new ChangeTravelSupplierPaymentStatusCommand(PaymentId, new ChangeTravelPaymentStatusDto("Completed", null)), default),
            () => new ReconcileTravelSupplierPaymentCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new ReconcileTravelSupplierPaymentCommand(PaymentId, new ReconcileTravelPaymentDto("BANK-1", null)), default),
            () => new GetTravelSupplierPaymentByIdQueryHandler(_payments.Object, Guard, _tenant.Object).Handle(new GetTravelSupplierPaymentByIdQuery(PaymentId), default),
            () => new IssueTravelSupplierCreditCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new IssueTravelSupplierCreditCommand(new IssueTravelSupplierCreditDto(Guid.NewGuid(), BillId, 10, "x", null, null)), default),
            () => new RecordTravelSupplierRefundCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new RecordTravelSupplierRefundCommand(new RecordTravelSupplierRefundDto(Guid.NewGuid(), null, PaymentId, 10, "Cash", null, DateTime.UtcNow, null, null)), default),
        };
        foreach (var call in calls) await call.Should().ThrowAsync<NotFoundException>();
        _bills.Verify(b => b.UpdateDraftAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TravelSupplierBillHeaderWrite>(),
            It.IsAny<IReadOnlyList<TravelSupplierBillLineWrite>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Creating_a_bill_for_another_tenants_supplier_is_not_found()
    {
        var act = () => new CreateTravelSupplierBillCommandHandler(_bills.Object, _suppliers.Object, _tenant.Object, _user.Object)
            .Handle(new CreateTravelSupplierBillCommand(new CreateTravelSupplierBillDto(999, "SAR", null, null, null, null,
                [new SaveTravelSupplierBillLineDto("Coach", null, null, null, null, 100, 0)])), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Recording_a_payment_to_another_tenants_supplier_is_not_found()
    {
        var act = () => new RecordTravelSupplierPaymentCommandHandler(_payments.Object, _bills.Object, _suppliers.Object, _tenant.Object, _user.Object)
            .Handle(new RecordTravelSupplierPaymentCommand(new RecordTravelSupplierPaymentDto(Guid.NewGuid(), 999, DateTime.UtcNow, "Cash", null, 10, "SAR", true, null, [])), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Bills ──

    [Fact]
    public async Task A_supplier_invoice_number_cannot_be_entered_twice()
    {
        _bills.Setup(b => b.SupplierReferenceInUseAsync(Tenant, SupplierId, "INV-8812", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await new CreateTravelSupplierBillCommandHandler(_bills.Object, _suppliers.Object, _tenant.Object, _user.Object)
            .Handle(new CreateTravelSupplierBillCommand(new CreateTravelSupplierBillDto(SupplierId, "SAR", " INV-8812 ", null, null, null,
                [new SaveTravelSupplierBillLineDto("Coach", null, null, null, null, 100, 0)])), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("INV-8812");
        _bills.Verify(b => b.CreateAsync(It.IsAny<int>(), It.IsAny<TravelSupplierBillHeaderWrite>(), It.IsAny<IReadOnlyList<TravelSupplierBillLineWrite>>(),
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_manual_line_tagged_to_another_tenants_booking_is_refused()
    {
        _bills.Setup(b => b.GetBillableCostsAsync(Tenant, SupplierId, "SAR", null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _bills.Setup(b => b.ContextExistsAsync(Tenant, "Booking", 555, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var result = await new CreateTravelSupplierBillCommandHandler(_bills.Object, _suppliers.Object, _tenant.Object, _user.Object)
            .Handle(new CreateTravelSupplierBillCommand(new CreateTravelSupplierBillDto(SupplierId, "SAR", null, null, null, null,
                [new SaveTravelSupplierBillLineDto("Coach", null, null, "Booking", 555, 100, 0)])), default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Creating_a_bill_writes_gross_commission_and_net()
    {
        _bills.Setup(b => b.GetBillableCostsAsync(Tenant, SupplierId, "SAR", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelBillableCostDto { SourceType = "Hotel", SourceId = 1, SupplierId = SupplierId, Description = "Hilton", Amount = 8000, CurrencyCode = "SAR" }]);
        TravelSupplierBillHeaderWrite? written = null;
        _bills.Setup(b => b.CreateAsync(Tenant, It.IsAny<TravelSupplierBillHeaderWrite>(), It.IsAny<IReadOnlyList<TravelSupplierBillLineWrite>>(), 7, It.IsAny<CancellationToken>()))
            .Callback<int, TravelSupplierBillHeaderWrite, IReadOnlyList<TravelSupplierBillLineWrite>, int?, CancellationToken>((_, h, _, _, _) => written = h)
            .ReturnsAsync(BillId);

        var result = await new CreateTravelSupplierBillCommandHandler(_bills.Object, _suppliers.Object, _tenant.Object, _user.Object)
            .Handle(new CreateTravelSupplierBillCommand(new CreateTravelSupplierBillDto(SupplierId, "sar", "INV-8812", null, null, null,
                [new SaveTravelSupplierBillLineDto("", "Hotel", 1, null, null, 8000, 400)])), default);

        result.Data.Should().Be(BillId);
        written!.Gross.Should().Be(8000);
        written.CommissionTotal.Should().Be(400);
        written.Net.Should().Be(7600);
        written.CurrencyCode.Should().Be("SAR");
    }

    [Fact]
    public async Task A_paid_bill_cannot_be_voided()
    {
        Bill(Posted(paid: 4000));
        var result = await new VoidTravelSupplierBillCommandHandler(_bills.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new VoidTravelSupplierBillCommand(BillId, "Duplicate"), default);
        result.Success.Should().BeFalse();
        _bills.Verify(b => b.VoidAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Billable_costs_carry_the_suggested_commission()
    {
        _bills.Setup(b => b.GetBillableCostsAsync(Tenant, SupplierId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelBillableCostDto { SourceType = "Hotel", SourceId = 1, Amount = 8000, CurrencyCode = "SAR", Units = 20 }]);
        _setup.Setup(s => s.GetCommissionRulesAsync(Tenant, SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelSupplierCommissionRuleDto { Id = 1, ServiceType = "Hotel", Basis = "PercentOfCost", Rate = 5, ValidFrom = new DateTime(2020, 1, 1), IsActive = true }]);

        var result = await new GetTravelSupplierBillableQueryHandler(_bills.Object, _suppliers.Object, _setup.Object, Guard, _tenant.Object)
            .Handle(new GetTravelSupplierBillableQuery(SupplierId, null, null, null), default);
        result.Data!.Single().SuggestedCommission.Should().Be(400);
    }

    // ── Payments, credits, refunds ──

    [Fact]
    public async Task A_payment_cannot_be_allocated_beyond_a_bills_open_balance()
    {
        _bills.Setup(b => b.GetOpenBillsAsync(Tenant, SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelSupplierBillListItemDto { Id = BillId, CurrencyCode = "SAR", Status = "Posted", Total = 3600 }]);
        var result = await new RecordTravelSupplierPaymentCommandHandler(_payments.Object, _bills.Object, _suppliers.Object, _tenant.Object, _user.Object)
            .Handle(new RecordTravelSupplierPaymentCommand(new RecordTravelSupplierPaymentDto(Guid.NewGuid(), SupplierId, DateTime.UtcNow, "BankTransfer", null, 5000, "SAR", true,
                null, [new TravelAllocationInputDto(BillId, 5000, null)])), default);

        result.Success.Should().BeFalse();
        _payments.Verify(p => p.RecordAsync(It.IsAny<int>(), It.IsAny<TravelPaymentWrite>(), It.IsAny<IReadOnlyList<TravelAllocationWrite>>(), It.IsAny<int?>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_supplier_credit_cannot_exceed_what_is_left_to_credit()
    {
        Bill(Posted(net: 1000, credited: 800));
        var result = await new IssueTravelSupplierCreditCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new IssueTravelSupplierCreditCommand(new IssueTravelSupplierCreditDto(Guid.NewGuid(), BillId, 300, "Room downgrade", null, null)), default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("200");
    }

    [Fact]
    public async Task A_credit_linked_to_another_suppliers_cancellation_is_refused()
    {
        Bill(Posted());
        _payments.Setup(p => p.GetCancellationSupplierAsync(Tenant, 88, It.IsAny<CancellationToken>())).ReturnsAsync((99, "Pending", "SAR"));
        var result = await new IssueTravelSupplierCreditCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new IssueTravelSupplierCreditCommand(new IssueTravelSupplierCreditDto(Guid.NewGuid(), BillId, 100, "Cancelled", 88, null)), default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task A_credit_for_a_pending_cancellation_moves_it_to_received_from_supplier()
    {
        Bill(Posted());
        _payments.Setup(p => p.GetCancellationSupplierAsync(Tenant, 88, It.IsAny<CancellationToken>())).ReturnsAsync((SupplierId, "Pending", "SAR"));
        _payments.Setup(p => p.IssueCreditAsync(Tenant, It.IsAny<IssueTravelSupplierCreditDto>(), It.IsAny<DateTime>(), 7, It.IsAny<DateTime>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelFinanceWriteResult(5, null));
        var result = await new IssueTravelSupplierCreditCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new IssueTravelSupplierCreditCommand(new IssueTravelSupplierCreditDto(Guid.NewGuid(), BillId, 100, "Cancelled", 88, null)), default);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task A_refund_is_received_only_when_the_supplier_owes_us()
    {
        Bill(Posted(net: 1000, paid: 1000));
        var result = await new RecordTravelSupplierRefundCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new RecordTravelSupplierRefundCommand(new RecordTravelSupplierRefundDto(Guid.NewGuid(), BillId, null, 50, "BankTransfer", null, DateTime.UtcNow, null, null)), default);
        result.Success.Should().BeFalse();
    }

    // ── Reports ──

    [Fact]
    public void Payables_summary_counts_are_zero_filled_and_sum()
    {
        var summary = GetTravelPayablesSummaryQueryHandler.Build([], new Dictionary<string, int> { ["Posted"] = 3 });
        summary.BillCounts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Draft"] = 0, ["Posted"] = 3, ["Void"] = 0 });
        summary.BillTotal.Should().Be(3);
    }

    [Fact]
    public async Task An_inverted_bill_date_range_is_empty_without_querying()
    {
        var result = await new GetTravelSupplierBillsQueryHandler(_bills.Object, _scope.Object, _tenant.Object)
            .Handle(new GetTravelSupplierBillsQuery { BillFrom = new DateTime(2027, 3, 10), BillTo = new DateTime(2027, 3, 1) }, default);
        result.Data!.Items.Should().BeEmpty();
        _bills.Verify(b => b.GetPagedAsync(It.IsAny<int>(), It.IsAny<TravelBranchScope>(), It.IsAny<TravelSupplierBillListFilter>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Commissions ──

    [Fact]
    public async Task A_commission_rule_for_another_tenants_user_is_not_found()
    {
        _commissions.Setup(c => c.UserExistsAsync(Tenant, 500, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var act = () => new SaveTravelCommissionRuleCommandHandler(_commissions.Object, _tenant.Object, _user.Object)
            .Handle(new SaveTravelCommissionRuleCommand(null, new SaveTravelCommissionRuleDto("Staff", 500, null, "All", "PercentOfMargin", 10, DateTime.UtcNow, null, true)), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task A_payout_above_the_payable_balance_is_refused()
    {
        _commissions.Setup(c => c.SubAgentExistsAsync(Tenant, 3, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _commissions.Setup(c => c.GetAttributedInvoicesAsync(Tenant, "SubAgent", 3, null, null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _commissions.Setup(c => c.GetPayoutsAsync(Tenant, "SubAgent", 3, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var result = await new RecordTravelCommissionPayoutCommandHandler(_commissions.Object, Service, _tenant.Object, _user.Object)
            .Handle(new RecordTravelCommissionPayoutCommand(new RecordTravelCommissionPayoutDto(Guid.NewGuid(), "SubAgent", 3, DateTime.UtcNow, "Cash", null, 100, "PKR", null)), default);

        result.Success.Should().BeFalse();
        _commissions.Verify(c => c.RecordPayoutAsync(It.IsAny<int>(), It.IsAny<RecordTravelCommissionPayoutDto>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_payout_within_the_balance_is_recorded_with_the_gross_payable_as_its_cap()
    {
        // Sub-agent 3 earns 10 % of sell on a settled 1,000 PKR invoice (payable 100); 60 was paid out already.
        _commissions.Setup(c => c.SubAgentExistsAsync(Tenant, 3, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _commissions.Setup(c => c.GetAttributedInvoicesAsync(Tenant, "SubAgent", 3, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelAttributedInvoiceFact(10, "TINV-1", new DateTime(2027, 2, 1), 5, "Khan family", "PKR", 0, null, null, 3, "Haramain Tours")]);
        _facts.Setup(f => f.GetInvoiceContextKeysAsync(Tenant, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(["Booking:1"]);
        _facts.Setup(f => f.GetContextsByKeyAsync(Tenant, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelContextFact("Booking", 1, "BKG-1", new DateTime(2027, 3, 1), null, null, null, null)]);
        _facts.Setup(f => f.GetRevenueFactsAsync(Tenant, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelRevenueFact(10, "TINV-1", new DateTime(2027, 2, 1), "PKR", 1000, 0, 1000, "Flight", "Booking", 1, "Flight")]);
        _facts.Setup(f => f.GetInvoiceCreditsAsync(Tenant, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _facts.Setup(f => f.GetCostFactsAsync(Tenant, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _facts.Setup(f => f.GetBillCreditsAsync(Tenant, It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _commissions.Setup(c => c.GetRulesAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(
            [new TravelCommissionRuleDto { Id = 1, BeneficiaryType = "SubAgent", SubAgentId = 3, Scope = "All", Basis = "PercentOfSell", Rate = 10, ValidFrom = new DateTime(2027, 1, 1), IsActive = true }]);
        _commissions.Setup(c => c.GetPayoutsAsync(Tenant, "SubAgent", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelCommissionPayoutDto { Id = 1, BeneficiaryType = "SubAgent", BeneficiaryId = 3, CurrencyCode = "PKR", Amount = 60 }]);
        _commissions.Setup(c => c.RecordPayoutAsync(Tenant, It.IsAny<RecordTravelCommissionPayoutDto>(), It.IsAny<decimal>(), 7, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelFinanceWriteResult(9, null));
        var handler = new RecordTravelCommissionPayoutCommandHandler(_commissions.Object, Service, _tenant.Object, _user.Object);

        var ok = await handler.Handle(new RecordTravelCommissionPayoutCommand(new RecordTravelCommissionPayoutDto(Guid.NewGuid(), "SubAgent", 3, DateTime.UtcNow, "Cash", null, 40, "PKR", null)), default);
        var tooMuch = await handler.Handle(new RecordTravelCommissionPayoutCommand(new RecordTravelCommissionPayoutDto(Guid.NewGuid(), "SubAgent", 3, DateTime.UtcNow, "Cash", null, 41, "PKR", null)), default);

        ok.Success.Should().BeTrue();
        tooMuch.Success.Should().BeFalse();
        tooMuch.Message.Should().Contain("40");
        // The repository re-checks against committed payouts under its lock, so it gets the gross payable (100), not the remaining 40.
        _commissions.Verify(c => c.RecordPayoutAsync(Tenant, It.Is<RecordTravelCommissionPayoutDto>(d => d.Amount == 40), 100m, 7, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _commissions.Verify(c => c.RecordPayoutAsync(It.IsAny<int>(), It.IsAny<RecordTravelCommissionPayoutDto>(), It.IsAny<decimal>(), It.IsAny<int?>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reassigning_another_tenants_invoice_is_not_found()
    {
        _invoices.Setup(i => i.GetStateAsync(31, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>())).ReturnsAsync((TravelInvoiceState?)null);
        var act = () => new UpdateTravelInvoiceAttributionCommandHandler(_commissions.Object, new TravelFinanceGuard(_invoices.Object, Mock.Of<ITravelPaymentRepository>(), _scope.Object),
                _tenant.Object, _user.Object)
            .Handle(new UpdateTravelInvoiceAttributionCommand(31, new UpdateTravelInvoiceAttributionDto(7, null)), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Commission_lines_for_an_inverted_issue_range_are_empty()
    {
        var result = await new GetTravelCommissionLinesQueryHandler(Service, _tenant.Object)
            .Handle(new GetTravelCommissionLinesQuery(null, null, null, new DateTime(2027, 3, 10), new DateTime(2027, 3, 1)), default);
        result.Data.Should().BeEmpty();
        _commissions.Verify(c => c.GetAttributedInvoicesAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Profitability ──

    [Fact]
    public async Task Profitability_needs_a_reporting_currency()
    {
        _config.Setup(c => c.GetSettingsAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelSettingsDto());
        var result = await new GetTravelProfitabilityQueryHandler(_facts.Object, Service, _scope.Object, _tenant.Object)
            .Handle(new GetTravelProfitabilityQuery("Booking", new DateTime(2027, 1, 1), new DateTime(2027, 12, 31), null, null), default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task An_inverted_travel_range_is_empty_without_querying()
    {
        var result = await new GetTravelProfitabilityQueryHandler(_facts.Object, Service, _scope.Object, _tenant.Object)
            .Handle(new GetTravelProfitabilityQuery("Group", new DateTime(2027, 3, 10), new DateTime(2027, 3, 1), null, null), default);
        result.Data!.Contexts.Should().BeEmpty();
        result.Data.ReportingCurrencyCode.Should().Be("PKR");
        _facts.Verify(f => f.GetContextsAsync(It.IsAny<int>(), It.IsAny<TravelBranchScope>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_context_outside_the_users_branches_is_not_found()
    {
        _scope.Setup(s => s.ResolveAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelBranchScope(false, [1]));
        _facts.Setup(f => f.GetContextsByKeyAsync(Tenant, It.Is<IReadOnlyList<string>>(k => k.Single() == "Booking:12"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TravelContextFact("Booking", 12, "BKG-12", null, 2, "Karachi", null, null)]);
        var act = () => new GetTravelContextProfitQueryHandler(_facts.Object, Service, _scope.Object, _tenant.Object)
            .Handle(new GetTravelContextProfitQuery("Booking", 12), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Another_tenants_context_is_not_found()
    {
        _facts.Setup(f => f.GetContextsByKeyAsync(Tenant, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var act = () => new GetTravelContextProfitQueryHandler(_facts.Object, Service, _scope.Object, _tenant.Object)
            .Handle(new GetTravelContextProfitQuery("Group", 3), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
