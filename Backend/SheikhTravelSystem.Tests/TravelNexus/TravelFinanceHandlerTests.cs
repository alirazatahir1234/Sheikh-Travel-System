using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Queries;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelFinanceHandlerTests
{
    private const int Tenant = 9;
    private const int CustomerId = 5;
    private const int InvoiceId = 31;
    private const int PaymentId = 41;

    private readonly Mock<ITravelInvoiceRepository> _invoices = new();
    private readonly Mock<ITravelPaymentRepository> _payments = new();
    private readonly Mock<ITravelCustomerRepository> _customers = new();
    private readonly Mock<ITravelConfigRepository> _config = new();
    private readonly Mock<ITravelGroupRepository> _groups = new();
    private readonly Mock<ITravelBranchScopeResolver> _scope = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ICurrentUserService> _user = new();

    public TravelFinanceHandlerTests()
    {
        _tenant.Setup(t => t.GetRequiredTenantId()).Returns(Tenant);
        _user.Setup(u => u.UserId).Returns(7);
        _scope.Setup(s => s.ResolveAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(TravelBranchScope.CompanyWide);
        _customers.Setup(c => c.GetByIdAsync(CustomerId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelCustomerDto { CustomerId = CustomerId, FullName = "Khan family", BranchId = 3 });
        _config.Setup(c => c.GetSettingsAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelSettingsDto { DefaultPaymentTermsDays = 7 });
    }

    private TravelFinanceGuard Guard => new(_invoices.Object, _payments.Object, _scope.Object);

    private void GivenInvoice(string status = "Issued", decimal total = 160_000, decimal paid = 0, decimal credited = 0, decimal refunded = 0,
        int allocations = 0, int creditNotes = 0, int refunds = 0, int installments = 0, decimal installmentTotal = 0, string currency = "PKR",
        DateTime? firstInstallment = null, DateTime? lastInstallment = null)
        => _invoices.Setup(i => i.GetStateAsync(InvoiceId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelInvoiceState
            {
                Id = InvoiceId, Status = status, CustomerId = CustomerId, CurrencyCode = currency, Total = total, Paid = paid, Credited = credited,
                Refunded = refunded, AllocationCount = allocations, CreditNoteCount = creditNotes, RefundCount = refunds, InstallmentCount = installments,
                InstallmentTotal = installmentTotal, FirstInstallmentDate = firstInstallment, LastInstallmentDate = lastInstallment, BranchId = 3
            });

    private void GivenPayment(string status = "Completed", decimal amount = 100_000, decimal allocated = 0, decimal refunded = 0, DateTime? reconciled = null)
        => _payments.Setup(p => p.GetStateAsync(PaymentId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelPaymentState
            {
                Id = PaymentId, CustomerId = CustomerId, Amount = amount, CurrencyCode = "PKR", Status = status, Allocated = allocated, Refunded = refunded,
                ReconciledAt = reconciled, Method = "BankTransfer"
            });

    private void GivenOpenInvoices(params TravelInvoiceListItemDto[] open)
        => _invoices.Setup(i => i.GetOpenInvoicesAsync(Tenant, CustomerId, It.IsAny<CancellationToken>())).ReturnsAsync(open);

    private static TravelBillableItemDto Flight(decimal amount = 160_000, string currency = "PKR") => new()
    {
        SourceType = "Flight", SourceId = 300, CustomerId = CustomerId, Amount = amount, CurrencyCode = currency, Description = "Flight PNR ABC123",
        ContextType = "Booking", ContextId = 70
    };

    // ── Tenant isolation ───────────────────────────────────────────────────

    [Fact]
    public async Task Another_tenants_invoice_payment_or_customer_is_not_found()
    {
        // State lookups are tenant- and branch-scoped in SQL; a foreign id yields null.
        var calls = new Func<Task>[]
        {
            () => new GetTravelInvoiceByIdQueryHandler(_invoices.Object, Guard, _tenant.Object).Handle(new GetTravelInvoiceByIdQuery(InvoiceId), default),
            () => new UpdateTravelInvoiceCommandHandler(_invoices.Object, Mock.Of<ITravelCommissionRepository>(), Guard, _tenant.Object, _user.Object)
                .Handle(new UpdateTravelInvoiceCommand(InvoiceId, new UpdateTravelInvoiceDto(null, null, 0, null, [new("Fee", 1, 1, null, null)], [])), default),
            () => new IssueTravelInvoiceCommandHandler(_invoices.Object, _config.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new IssueTravelInvoiceCommand(InvoiceId, new IssueTravelInvoiceDto(null, null)), default),
            () => new VoidTravelInvoiceCommandHandler(_invoices.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new VoidTravelInvoiceCommand(InvoiceId, new VoidTravelInvoiceDto("x")), default),
            () => new DeleteTravelInvoiceCommandHandler(_invoices.Object, Guard, _tenant.Object).Handle(new DeleteTravelInvoiceCommand(InvoiceId), default),
            () => new AllocateTravelPaymentCommandHandler(_payments.Object, _invoices.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new AllocateTravelPaymentCommand(PaymentId, new AllocateTravelPaymentDto([new(InvoiceId, 1, null)])), default),
            () => new ChangeTravelPaymentStatusCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new ChangeTravelPaymentStatusCommand(PaymentId, new ChangeTravelPaymentStatusDto("Completed", null)), default),
            () => new ReconcileTravelPaymentCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new ReconcileTravelPaymentCommand(PaymentId, new ReconcileTravelPaymentDto("BANK-1", null)), default),
            () => new GetTravelPaymentByIdQueryHandler(_payments.Object, Guard, _tenant.Object).Handle(new GetTravelPaymentByIdQuery(PaymentId), default),
            () => new IssueTravelCreditNoteCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new IssueTravelCreditNoteCommand(new IssueTravelCreditNoteDto(Guid.NewGuid(), InvoiceId, 10, "x", null, null)), default),
            () => new RecordTravelRefundCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new RecordTravelRefundCommand(new RecordTravelRefundDto(Guid.NewGuid(), null, PaymentId, 10, "Cash", null, DateTime.UtcNow, null, null)), default),
            () => new CreateTravelInvoiceCommandHandler(_invoices.Object, _customers.Object, Mock.Of<ITravelCommissionRepository>(), _scope.Object, _tenant.Object, _user.Object)
                .Handle(new CreateTravelInvoiceCommand(new CreateTravelInvoiceDto(99, "PKR", null, null, 0, null, [new("Fee", 1, 1, null, null)], [])), default),
            () => new RecordTravelPaymentCommandHandler(_payments.Object, _invoices.Object, _customers.Object, _scope.Object, _tenant.Object, _user.Object)
                .Handle(new RecordTravelPaymentCommand(new RecordTravelPaymentDto(Guid.NewGuid(), 99, DateTime.UtcNow, "Cash", null, 10, "PKR", true, null, [])), default),
            () => new GetTravelCustomerStatementQueryHandler(_invoices.Object, _customers.Object, _scope.Object, _tenant.Object)
                .Handle(new GetTravelCustomerStatementQuery(99, null, null, null), default),
            () => new GetTravelGroupFinanceQueryHandler(_invoices.Object, _groups.Object, _scope.Object, _tenant.Object)
                .Handle(new GetTravelGroupFinanceQuery(60), default),
        };
        foreach (var call in calls) await call.Should().ThrowAsync<NotFoundException>();
        _invoices.Verify(i => i.GetDetailAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Billable_items_of_a_draft_belonging_to_another_customer_are_not_found()
    {
        GivenInvoice("Draft");
        _customers.Setup(c => c.GetByIdAsync(6, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelCustomerDto { CustomerId = 6, FullName = "Other" });
        var call = () => new GetTravelBillableItemsQueryHandler(_invoices.Object, _customers.Object, Guard, _scope.Object, _tenant.Object)
            .Handle(new GetTravelBillableItemsQuery(6, "PKR", InvoiceId), default);
        await call.Should().ThrowAsync<NotFoundException>();
    }

    // ── Invoices ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_invoice_uses_billable_items_of_the_customer_in_the_invoice_currency()
    {
        var handler = new CreateTravelInvoiceCommandHandler(_invoices.Object, _customers.Object, Mock.Of<ITravelCommissionRepository>(), _scope.Object, _tenant.Object, _user.Object);
        var dto = new CreateTravelInvoiceDto(CustomerId, "pkr", null, null, 0, null, [new("", 1, 160_000, "Flight", 300)], []);

        _invoices.Setup(i => i.GetBillableAsync(Tenant, It.IsAny<TravelBranchScope>(), CustomerId, "PKR", null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        (await handler.Handle(new CreateTravelInvoiceCommand(dto), default)).Message.Should().Contain("already invoiced");

        _invoices.Setup(i => i.GetBillableAsync(Tenant, It.IsAny<TravelBranchScope>(), CustomerId, "PKR", null, It.IsAny<CancellationToken>())).ReturnsAsync([Flight()]);
        _invoices.Setup(i => i.CreateAsync(Tenant,
                It.Is<TravelInvoiceHeaderWrite>(h => h.Total == 160_000 && h.BookingId == 70 && h.BranchId == 3 && h.CurrencyCode == "PKR"),
                It.Is<IReadOnlyList<TravelInvoiceLineWrite>>(l => l.Count == 1 && l[0].Description == "Flight PNR ABC123" && l[0].SourceId == 300),
                It.IsAny<IReadOnlyList<SaveTravelInstallmentDto>>(), 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(InvoiceId);
        var ok = await handler.Handle(new CreateTravelInvoiceCommand(dto), default);
        ok.Success.Should().BeTrue(ok.Message);
        ok.Data.Should().Be(InvoiceId);
    }

    [Fact]
    public async Task Create_invoice_refuses_installments_that_do_not_add_up()
    {
        _invoices.Setup(i => i.GetBillableAsync(Tenant, It.IsAny<TravelBranchScope>(), CustomerId, "PKR", null, It.IsAny<CancellationToken>())).ReturnsAsync([Flight()]);
        var result = await new CreateTravelInvoiceCommandHandler(_invoices.Object, _customers.Object, Mock.Of<ITravelCommissionRepository>(), _scope.Object, _tenant.Object, _user.Object)
            .Handle(new CreateTravelInvoiceCommand(new CreateTravelInvoiceDto(CustomerId, "PKR", null, null, 0, null, [new("", 1, 160_000, "Flight", 300)],
                [new(new DateTime(2027, 1, 15), 100_000), new(new DateTime(2027, 2, 15), 50_000)])), default);
        result.Message.Should().Contain("add up to 150000");
        _invoices.Verify(i => i.CreateAsync(It.IsAny<int>(), It.IsAny<TravelInvoiceHeaderWrite>(), It.IsAny<IReadOnlyList<TravelInvoiceLineWrite>>(),
            It.IsAny<IReadOnlyList<SaveTravelInstallmentDto>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Issued_invoices_are_not_editable()
    {
        GivenInvoice("Issued");
        var result = await new UpdateTravelInvoiceCommandHandler(_invoices.Object, Mock.Of<ITravelCommissionRepository>(), Guard, _tenant.Object, _user.Object)
            .Handle(new UpdateTravelInvoiceCommand(InvoiceId, new UpdateTravelInvoiceDto(null, null, 0, null, [new("Fee", 1, 1, null, null)], [])), default);
        result.Message.Should().Contain("credit note");
    }

    [Fact]
    public async Task Issue_takes_payment_terms_or_the_last_installment_as_due_date()
    {
        var handler = new IssueTravelInvoiceCommandHandler(_invoices.Object, _config.Object, Guard, _tenant.Object, _user.Object);
        var issue = new DateTime(2027, 1, 10);

        GivenInvoice("Draft");
        _invoices.Setup(i => i.IssueAsync(Tenant, InvoiceId, issue, issue.AddDays(7), 7, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync("TINV-2027-00001");
        (await handler.Handle(new IssueTravelInvoiceCommand(InvoiceId, new IssueTravelInvoiceDto(issue, null)), default)).Data.Should().Be("TINV-2027-00001");

        GivenInvoice("Draft", installments: 2, installmentTotal: 150_000);
        (await handler.Handle(new IssueTravelInvoiceCommand(InvoiceId, new IssueTravelInvoiceDto(issue, null)), default)).Message.Should().Contain("no longer add up");

        GivenInvoice("Draft", installments: 2, installmentTotal: 160_000, firstInstallment: issue.AddDays(-1), lastInstallment: issue.AddDays(30));
        (await handler.Handle(new IssueTravelInvoiceCommand(InvoiceId, new IssueTravelInvoiceDto(issue, null)), default)).Message.Should().Contain("before the issue date");

        GivenInvoice("Draft", installments: 2, installmentTotal: 160_000, firstInstallment: issue, lastInstallment: issue.AddDays(30));
        _invoices.Setup(i => i.IssueAsync(Tenant, InvoiceId, issue, issue.AddDays(30), 7, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync("TINV-2027-00002");
        (await handler.Handle(new IssueTravelInvoiceCommand(InvoiceId, new IssueTravelInvoiceDto(issue, null)), default)).Data.Should().Be("TINV-2027-00002");
    }

    [Fact]
    public async Task Void_is_refused_once_money_moved()
    {
        var handler = new VoidTravelInvoiceCommandHandler(_invoices.Object, Guard, _tenant.Object, _user.Object);
        GivenInvoice(paid: 10, allocations: 1);
        (await handler.Handle(new VoidTravelInvoiceCommand(InvoiceId, new VoidTravelInvoiceDto("Wrong customer")), default)).Message.Should().Contain("credit note instead");
        GivenInvoice(creditNotes: 1, credited: 10);
        (await handler.Handle(new VoidTravelInvoiceCommand(InvoiceId, new VoidTravelInvoiceDto("Wrong customer")), default)).Success.Should().BeFalse();
        _invoices.Verify(i => i.VoidAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);

        GivenInvoice();
        _invoices.Setup(i => i.VoidAsync(Tenant, InvoiceId, "Wrong customer", 7, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        (await handler.Handle(new VoidTravelInvoiceCommand(InvoiceId, new VoidTravelInvoiceDto(" Wrong customer ")), default)).Success.Should().BeTrue();
    }

    // ── Payments ───────────────────────────────────────────────────────────

    private RecordTravelPaymentCommandHandler RecordHandler()
        => new(_payments.Object, _invoices.Object, _customers.Object, _scope.Object, _tenant.Object, _user.Object);

    [Fact]
    public async Task A_payment_is_applied_across_currencies_at_the_recorded_rate()
    {
        GivenOpenInvoices(new TravelInvoiceListItemDto { Id = InvoiceId, CurrencyCode = "SAR", Total = 4_000, Status = "Issued" });
        var key = Guid.NewGuid();
        _payments.Setup(p => p.RecordAsync(Tenant,
                It.Is<TravelPaymentWrite>(w => w.IdempotencyKey == key && w.Status == "Completed" && w.Amount == 300_000 && w.CurrencyCode == "PKR" && w.BranchId == 3),
                It.Is<IReadOnlyList<TravelAllocationWrite>>(a => a.Count == 1 && a[0].AmountApplied == 3_990 && a[0].ExchangeRate == 0.0133m),
                7, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelFinanceWriteResult(PaymentId, null));
        var result = await RecordHandler().Handle(new RecordTravelPaymentCommand(new RecordTravelPaymentDto(key, CustomerId, DateTime.UtcNow.Date, "Cash", null,
            300_000, "pkr", true, null, [new(InvoiceId, 300_000, 0.0133m)])), default);
        result.Success.Should().BeTrue(result.Message);
        result.Data.Should().Be(PaymentId);
    }

    [Fact]
    public async Task A_payment_cannot_be_applied_above_the_open_balance()
    {
        GivenOpenInvoices(new TravelInvoiceListItemDto { Id = InvoiceId, CurrencyCode = "PKR", Total = 160_000, Paid = 100_000, PendingPaid = 50_000, Status = "Issued" });
        var result = await RecordHandler().Handle(new RecordTravelPaymentCommand(new RecordTravelPaymentDto(Guid.NewGuid(), CustomerId, DateTime.UtcNow.Date,
            "Cash", null, 20_000, "PKR", true, null, [new(InvoiceId, 20_000, null)])), default);
        result.Message.Should().Contain("open balance (10000");
    }

    [Fact]
    public async Task A_resubmitted_payment_returns_the_original_receipt()
    {
        GivenOpenInvoices();
        _payments.Setup(p => p.RecordAsync(Tenant, It.IsAny<TravelPaymentWrite>(), It.IsAny<IReadOnlyList<TravelAllocationWrite>>(), 7, It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new TravelFinanceWriteResult(PaymentId, null, Replayed: true));
        var result = await RecordHandler().Handle(new RecordTravelPaymentCommand(new RecordTravelPaymentDto(Guid.NewGuid(), CustomerId, DateTime.UtcNow.Date,
            "Cheque", "CHQ 1001", 50_000, "PKR", false, null, [])), default);
        result.Data.Should().Be(PaymentId);
        result.Message.Should().Contain("already recorded");
        _payments.Verify(p => p.RecordAsync(Tenant, It.Is<TravelPaymentWrite>(w => w.Status == "Pending"), It.IsAny<IReadOnlyList<TravelAllocationWrite>>(), 7,
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reconciled_or_refunded_payments_cannot_be_marked_failed()
    {
        var handler = new ChangeTravelPaymentStatusCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object);
        GivenPayment("Completed", reconciled: DateTime.UtcNow);
        (await handler.Handle(new ChangeTravelPaymentStatusCommand(PaymentId, new ChangeTravelPaymentStatusDto("Failed", "Bounced")), default))
            .Message.Should().Contain("reconciled or refunded");
        GivenPayment("Failed");
        (await handler.Handle(new ChangeTravelPaymentStatusCommand(PaymentId, new ChangeTravelPaymentStatusDto("Completed", null)), default))
            .Success.Should().BeFalse();

        GivenPayment("Pending");
        _payments.Setup(p => p.ChangeStatusAsync(Tenant, PaymentId, "Pending", "Completed", null, 7, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        (await handler.Handle(new ChangeTravelPaymentStatusCommand(PaymentId, new ChangeTravelPaymentStatusDto("Completed", null)), default)).Success.Should().BeTrue();
    }

    // ── Credit notes and refunds ───────────────────────────────────────────

    [Fact]
    public async Task Credit_notes_are_capped_and_must_match_the_cancellation_customer()
    {
        var handler = new IssueTravelCreditNoteCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object);
        GivenInvoice(total: 160_000, credited: 150_000, creditNotes: 1);
        (await handler.Handle(new IssueTravelCreditNoteCommand(new IssueTravelCreditNoteDto(Guid.NewGuid(), InvoiceId, 20_000, "Cancelled", null, null)), default))
            .Message.Should().Contain("At most 10000");

        GivenInvoice();
        _payments.Setup(p => p.GetCancellationFactsAsync(Tenant, 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelCancellationFacts(55, 70, 6, "Pending", 20_000, "PKR"));
        (await handler.Handle(new IssueTravelCreditNoteCommand(new IssueTravelCreditNoteDto(Guid.NewGuid(), InvoiceId, 20_000, "Cancelled", 55, null)), default))
            .Message.Should().Contain("another customer");
    }

    [Fact]
    public async Task Refunds_are_capped_by_the_credit_balance_and_move_the_cancellation()
    {
        var handler = new RecordTravelRefundCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object);
        GivenInvoice(total: 160_000, paid: 160_000);
        (await handler.Handle(new RecordTravelRefundCommand(new RecordTravelRefundDto(Guid.NewGuid(), InvoiceId, null, 5_000, "Cash", null, DateTime.UtcNow, null, null)), default))
            .Message.Should().Contain("credit note first");

        GivenInvoice(total: 160_000, paid: 160_000, credited: 20_000, creditNotes: 1, allocations: 1);
        (await handler.Handle(new RecordTravelRefundCommand(new RecordTravelRefundDto(Guid.NewGuid(), InvoiceId, null, 25_000, "Cash", null, DateTime.UtcNow, null, null)), default))
            .Message.Should().Contain("Only 20000");

        _payments.Setup(p => p.GetCancellationFactsAsync(Tenant, 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelCancellationFacts(55, 70, CustomerId, "Pending", 20_000, "PKR"));
        _payments.Setup(p => p.RecordRefundAsync(Tenant, It.Is<RecordTravelRefundDto>(r => r.Amount == 20_000), CustomerId, "PKR", 7, It.IsAny<DateTime>(), true,
            It.IsAny<CancellationToken>())).ReturnsAsync(new TravelFinanceWriteResult(77, null));
        var ok = await handler.Handle(new RecordTravelRefundCommand(new RecordTravelRefundDto(Guid.NewGuid(), InvoiceId, null, 20_000, "BankTransfer", "TRX 9",
            DateTime.UtcNow, 55, null)), default);
        ok.Success.Should().BeTrue(ok.Message);
        ok.Message.Should().Contain("paid to the customer");
    }

    [Fact]
    public async Task Advance_refunds_are_capped_by_the_unallocated_amount()
    {
        GivenPayment("Completed", amount: 100_000, allocated: 70_000);
        var result = await new RecordTravelRefundCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new RecordTravelRefundCommand(new RecordTravelRefundDto(Guid.NewGuid(), null, PaymentId, 40_000, "Cash", null, DateTime.UtcNow, null, null)), default);
        result.Message.Should().Contain("Only 30000");
    }

    [Fact]
    public async Task Credit_notes_behind_a_refund_cannot_be_voided()
    {
        _payments.Setup(p => p.GetCreditNoteAsync(8, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelCreditNoteState(8, InvoiceId, "Issued", 20_000));
        GivenInvoice(refunds: 1, credited: 20_000, creditNotes: 1);
        var result = await new VoidTravelCreditNoteCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new VoidTravelCreditNoteCommand(8, new VoidTravelCreditNoteDto("Issued in error")), default);
        result.Message.Should().Contain("refunded");
    }

    // ── Reports ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Statement_defaults_to_the_customers_first_currency_and_runs_balances()
    {
        _invoices.Setup(i => i.GetCustomerCurrenciesAsync(Tenant, CustomerId, It.IsAny<CancellationToken>())).ReturnsAsync(["PKR", "SAR"]);
        _invoices.Setup(i => i.GetStatementMovementsAsync(Tenant, CustomerId, "PKR", It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new TravelStatementMovement(new DateTime(2027, 1, 5), "Invoice", "TINV-1", "Invoice", 160_000, 0, InvoiceId, null, 1),
            new TravelStatementMovement(new DateTime(2027, 1, 9), "Payment", "TRCP-1", "Payment", 0, 100_000, InvoiceId, PaymentId, 3)
        ]);
        var handler = new GetTravelCustomerStatementQueryHandler(_invoices.Object, _customers.Object, _scope.Object, _tenant.Object);
        var statement = (await handler.Handle(new GetTravelCustomerStatementQuery(CustomerId, "usd", null, null), default)).Data!;
        statement.CurrencyCode.Should().Be("PKR");
        statement.Currencies.Should().Equal("PKR", "SAR");
        statement.Entries.Select(e => e.Balance).Should().Equal(160_000, 60_000);
        statement.ClosingBalance.Should().Be(60_000);

        _invoices.Setup(i => i.GetCustomerCurrenciesAsync(Tenant, CustomerId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        (await handler.Handle(new GetTravelCustomerStatementQuery(CustomerId, null, null, null), default)).Data!.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Inverted_date_ranges_return_nothing()
    {
        var invoices = await new GetTravelInvoicesQueryHandler(_invoices.Object, _scope.Object, _tenant.Object)
            .Handle(new GetTravelInvoicesQuery { IssueFrom = new DateTime(2027, 2, 1), IssueTo = new DateTime(2027, 1, 1) }, default);
        invoices.Data!.Items.Should().BeEmpty();
        var payments = await new GetTravelPaymentsQueryHandler(_payments.Object, _scope.Object, _tenant.Object)
            .Handle(new GetTravelPaymentsQuery { ReceivedFrom = new DateTime(2027, 2, 1), ReceivedTo = new DateTime(2027, 1, 1) }, default);
        payments.Data!.Items.Should().BeEmpty();
        _invoices.Verify(i => i.GetPagedAsync(It.IsAny<int>(), It.IsAny<TravelBranchScope>(), It.IsAny<TravelInvoiceListFilter>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _payments.Verify(p => p.GetPagedAsync(It.IsAny<int>(), It.IsAny<TravelBranchScope>(), It.IsAny<TravelPaymentListFilter>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invoice_list_rows_get_payment_status_and_overdue_amounts()
    {
        var row = new TravelInvoiceListItemDto { Id = InvoiceId, Status = "Issued", Total = 160_000, Paid = 60_000, DueDate = DateTime.UtcNow.Date.AddDays(-3) };
        _invoices.Setup(i => i.GetPagedAsync(Tenant, It.IsAny<TravelBranchScope>(), It.Is<TravelInvoiceListFilter>(f => f.OverdueOnly), 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SheikhTravelSystem.Application.Common.PagedResult<TravelInvoiceListItemDto> { Items = [row], TotalCount = 1, Page = 1, PageSize = 20 });
        _invoices.Setup(i => i.GetInstallmentsAsync(It.IsAny<IReadOnlyList<int>>(), Tenant, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var result = await new GetTravelInvoicesQueryHandler(_invoices.Object, _scope.Object, _tenant.Object)
            .Handle(new GetTravelInvoicesQuery { Overdue = true, PageSize = 20 }, default);
        var item = result.Data!.Items.Single();
        item.PaymentStatus.Should().Be("PartiallyPaid");
        item.OverdueAmount.Should().Be(100_000);
    }

    // ── Idempotent retries return the original record (found by the live SQL run) ──

    [Fact]
    public async Task A_receipt_retried_after_it_settled_the_invoice_returns_the_original_receipt()
    {
        var key = Guid.NewGuid();
        GivenOpenInvoices(); // the first receipt paid the invoice: nothing is open any more
        _payments.Setup(p => p.FindByIdempotencyKeyAsync(Tenant, TravelIdempotentRecord.Receipt, key, It.IsAny<CancellationToken>())).ReturnsAsync(PaymentId);

        var result = await RecordHandler().Handle(new RecordTravelPaymentCommand(new RecordTravelPaymentDto(key, CustomerId, DateTime.UtcNow, "BankTransfer", null,
            160_000, "PKR", true, null, [new TravelAllocationInputDto(InvoiceId, 160_000, null)])), default);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(PaymentId);
        result.Message.Should().Contain("already recorded");
        _payments.Verify(p => p.RecordAsync(It.IsAny<int>(), It.IsAny<TravelPaymentWrite>(), It.IsAny<IReadOnlyList<TravelAllocationWrite>>(), It.IsAny<int?>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_credit_note_retried_after_it_used_up_the_creditable_amount_returns_the_original()
    {
        var key = Guid.NewGuid();
        GivenInvoice(total: 160_000, credited: 160_000);
        _payments.Setup(p => p.FindByIdempotencyKeyAsync(Tenant, TravelIdempotentRecord.CreditNote, key, It.IsAny<CancellationToken>())).ReturnsAsync(77);

        var result = await new IssueTravelCreditNoteCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new IssueTravelCreditNoteCommand(new IssueTravelCreditNoteDto(key, InvoiceId, 160_000, "Cancelled", null, null)), default);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(77);
        _payments.Verify(p => p.IssueCreditNoteAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<int?>(),
            It.IsAny<DateTime>(), It.IsAny<int?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_refund_retried_after_nothing_is_owed_back_returns_the_original()
    {
        var key = Guid.NewGuid();
        GivenInvoice(total: 160_000, paid: 160_000, credited: 10_000, refunded: 10_000); // balance back to zero
        _payments.Setup(p => p.FindByIdempotencyKeyAsync(Tenant, TravelIdempotentRecord.CustomerRefund, key, It.IsAny<CancellationToken>())).ReturnsAsync(88);

        var result = await new RecordTravelRefundCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new RecordTravelRefundCommand(new RecordTravelRefundDto(key, InvoiceId, null, 10_000, "Cash", null, DateTime.UtcNow, null, null)), default);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(88);
    }

    [Fact]
    public async Task A_replay_key_is_only_honoured_after_the_parent_is_found_in_the_callers_tenant_and_scope()
    {
        var key = Guid.NewGuid();
        _invoices.Setup(i => i.GetStateAsync(InvoiceId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>())).ReturnsAsync((TravelInvoiceState?)null);
        _payments.Setup(p => p.FindByIdempotencyKeyAsync(Tenant, It.IsAny<TravelIdempotentRecord>(), key, It.IsAny<CancellationToken>())).ReturnsAsync(77);

        var act = () => new IssueTravelCreditNoteCommandHandler(_payments.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new IssueTravelCreditNoteCommand(new IssueTravelCreditNoteDto(key, InvoiceId, 100, "x", null, null)), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
