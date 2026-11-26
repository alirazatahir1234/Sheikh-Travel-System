using FluentAssertions;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Queries;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelFinanceRulesTests
{
    private static readonly DateTime Today = new(2027, 2, 1);

    private static TravelInvoiceMoney Money(decimal total, decimal paid = 0, decimal credited = 0, decimal refunded = 0, decimal pending = 0)
        => new() { Total = total, Paid = paid, Credited = credited, Refunded = refunded, PendingPaid = pending };

    // ── Money arithmetic ───────────────────────────────────────────────────

    [Fact]
    public void Rounding_is_two_decimals_half_away_from_zero()
    {
        TravelFinanceRules.Round(10.005m).Should().Be(10.01m);
        TravelFinanceRules.Round(10.004m).Should().Be(10.00m);
        TravelFinanceRules.LineTotal(3, 33.335m).Should().Be(100.01m);
        TravelFinanceRules.Convert(300_000, 0.0133m).Should().Be(3_990m);
        TravelFinanceRules.Convert(1, 0.0133m).Should().Be(0.01m);
    }

    [Fact]
    public void Balance_counts_completed_payments_credits_and_refunds()
    {
        Money(160_000, paid: 100_000).Balance.Should().Be(60_000);
        Money(160_000, paid: 160_000, credited: 20_000).Balance.Should().Be(-20_000);
        Money(160_000, paid: 160_000, credited: 20_000, refunded: 20_000).Balance.Should().Be(0);
        Money(160_000, pending: 160_000).Balance.Should().Be(160_000, "pending cheques do not reduce the balance");
        TravelFinanceRules.Available(Money(160_000, paid: 40_000, pending: 100_000)).Should().Be(20_000);
        TravelFinanceRules.Available(Money(100, pending: 150)).Should().Be(0);
    }

    [Theory]
    [InlineData(100, 0, 0, 0, "Unpaid")]
    [InlineData(100, 40, 0, 0, "PartiallyPaid")]
    [InlineData(100, 0, 30, 0, "PartiallyPaid")]
    [InlineData(100, 100, 0, 0, "Paid")]
    [InlineData(100, 100, 20, 0, "CreditDue")]
    [InlineData(100, 100, 20, 20, "Paid")]
    public void Payment_status_is_derived_from_the_balance(decimal total, decimal paid, decimal credited, decimal refunded, string expected)
        => TravelFinanceRules.PaymentStatus("Issued", Money(total, paid, credited, refunded)).Should().Be(expected);

    [Fact]
    public void Drafts_and_voids_have_no_payment_status()
    {
        TravelFinanceRules.PaymentStatus("Draft", Money(100)).Should().BeNull();
        TravelFinanceRules.PaymentStatus("Void", Money(100)).Should().BeNull();
        TravelValues.InvoicePaymentStatuses.Should().Equal("Unpaid", "PartiallyPaid", "Paid", "CreditDue");
    }

    // ── Installments ───────────────────────────────────────────────────────

    private static TravelInstallmentDto Inst(int seq, DateTime due, decimal amount) => new() { InvoiceId = 1, Sequence = seq, DueDate = due, Amount = amount };

    [Fact]
    public void Installments_are_covered_oldest_due_first()
    {
        var schedule = new[] { Inst(2, new DateTime(2027, 2, 15), 60_000), Inst(1, new DateTime(2027, 1, 15), 100_000), Inst(3, new DateTime(2027, 3, 15), 40_000) };
        var covered = TravelFinanceRules.CoverInstallments(schedule, 130_000, Today);
        covered.Select(c => c.Sequence).Should().Equal(1, 2, 3);
        covered.Select(c => c.Status).Should().Equal("Paid", "PartlyPaid", "Due");
        covered[1].Covered.Should().Be(30_000);

        var late = TravelFinanceRules.CoverInstallments(schedule, 50_000, Today);
        late.Select(c => c.Status).Should().Equal("Overdue", "Due", "Due");
    }

    [Fact]
    public void Overdue_is_what_fell_due_before_today_minus_what_is_covered()
    {
        var schedule = new[] { Inst(1, new DateTime(2027, 1, 15), 100_000), Inst(2, new DateTime(2027, 2, 15), 60_000) };
        TravelFinanceRules.OverdueAmount(schedule, null, 160_000, 0, Today).Should().Be(100_000);
        TravelFinanceRules.OverdueAmount(schedule, null, 160_000, 100_000, Today).Should().Be(0);
        TravelFinanceRules.OverdueAmount(schedule, null, 160_000, 130_000, new DateTime(2027, 3, 1)).Should().Be(30_000);
        // Without a schedule the whole invoice falls due on its due date.
        TravelFinanceRules.OverdueAmount([], new DateTime(2027, 1, 31), 160_000, 60_000, Today).Should().Be(100_000);
        TravelFinanceRules.OverdueAmount([], new DateTime(2027, 2, 1), 160_000, 0, Today).Should().Be(0, "due today is not overdue");
    }

    [Fact]
    public void Next_due_date_skips_paid_installments()
    {
        var schedule = TravelFinanceRules.CoverInstallments(
            [Inst(1, new DateTime(2027, 1, 15), 100_000), Inst(2, new DateTime(2027, 2, 15), 60_000)], 100_000, Today);
        TravelFinanceRules.NextDueDate(schedule, null, 60_000).Should().Be(new DateTime(2027, 2, 15));
        TravelFinanceRules.NextDueDate([], new DateTime(2027, 2, 8), 10).Should().Be(new DateTime(2027, 2, 8));
        TravelFinanceRules.NextDueDate(schedule, null, 0).Should().BeNull();
    }

    [Fact]
    public void Installments_must_add_up_to_the_total()
    {
        var plan = new SaveTravelInstallmentDto[] { new(new DateTime(2027, 1, 15), 100_000), new(new DateTime(2027, 2, 15), 50_000) };
        TravelFinanceRules.ValidateInstallments(plan, 160_000, null).Should().Contain("add up to 150000").And.Contain("160000");
        TravelFinanceRules.ValidateInstallments(plan, 150_000, null).Should().BeNull();
        TravelFinanceRules.ValidateInstallments(plan, 150_000, new DateTime(2027, 2, 1)).Should().Contain("before the issue date");
        TravelFinanceRules.ValidateInstallments([new(new DateTime(2027, 1, 15), 50), new(new DateTime(2027, 1, 15), 50)], 100, null).Should().Contain("share a due date");
        TravelFinanceRules.ValidateInstallments([], 160_000, null).Should().BeNull();
    }

    // ── Allocations ────────────────────────────────────────────────────────

    private static readonly Dictionary<int, (string, decimal)> Open = new() { [1] = ("PKR", 160_000), [2] = ("SAR", 4_000) };

    [Fact]
    public void Same_currency_allocations_use_rate_one()
    {
        var (writes, error) = TravelFinanceRules.PlanAllocations([new(1, 100_000, null)], "PKR", 100_000, Open);
        error.Should().BeNull();
        writes.Should().ContainSingle().Which.Should().Be(new TravelAllocationWrite(1, 100_000, 1, 100_000));
        TravelFinanceRules.PlanAllocations([new(1, 100, 1.5m)], "PKR", 100, Open).Error.Should().Contain("rate of 1");
    }

    [Fact]
    public void Cross_currency_allocations_need_the_recorded_rate()
    {
        TravelFinanceRules.PlanAllocations([new(2, 300_000, null)], "PKR", 300_000, Open).Error.Should().Contain("exchange rate from PKR to SAR");
        var (writes, error) = TravelFinanceRules.PlanAllocations([new(2, 300_000, 0.0133m)], "PKR", 300_000, Open);
        error.Should().BeNull();
        writes[0].AmountApplied.Should().Be(3_990);
        writes[0].ExchangeRate.Should().Be(0.0133m);
    }

    [Fact]
    public void Allocations_never_exceed_the_invoice_balance_or_the_payment()
    {
        TravelFinanceRules.PlanAllocations([new(1, 170_000, null)], "PKR", 200_000, Open).Error.Should().Contain("more than the invoice's open balance");
        TravelFinanceRules.PlanAllocations([new(1, 100_000, null), new(2, 400_000, 0.0001m)], "PKR", 120_000, Open).Error.Should().Contain("exceed what is left");
        TravelFinanceRules.PlanAllocations([new(9, 10, null)], "PKR", 10, Open).Error.Should().Contain("not an open invoice");
        TravelFinanceRules.PlanAllocations([new(1, 10, null), new(1, 10, null)], "PKR", 20, Open).Error.Should().Contain("Each invoice once");
        TravelFinanceRules.PlanAllocations([new(2, 0.1m, 0.01m)], "PKR", 1, Open).Error.Should().Contain("rounds to zero");
    }

    // ── Invoice actions and payment actions ────────────────────────────────

    private static TravelInvoiceState State(string status, decimal total, decimal paid = 0, decimal credited = 0, decimal refunded = 0,
        int allocations = 0, int creditNotes = 0, int refunds = 0, decimal pending = 0) => new()
    {
        Status = status, Total = total, Paid = paid, Credited = credited, Refunded = refunded, PendingPaid = pending,
        AllocationCount = allocations, CreditNoteCount = creditNotes, RefundCount = refunds
    };

    [Fact]
    public void Invoice_actions_follow_status_and_balance()
    {
        TravelInvoiceStateMachine.AllowedActions(State("Draft", 100)).Should().Equal("Edit", "Issue", "Delete");
        TravelInvoiceStateMachine.AllowedActions(State("Issued", 100)).Should().Equal("RecordPayment", "CreditNote", "Void");
        TravelInvoiceStateMachine.AllowedActions(State("Issued", 100, paid: 40, allocations: 1)).Should().Equal("RecordPayment", "CreditNote");
        TravelInvoiceStateMachine.AllowedActions(State("Issued", 100, pending: 100, allocations: 1)).Should().Equal("CreditNote");
        TravelInvoiceStateMachine.AllowedActions(State("Issued", 100, paid: 100, credited: 20, allocations: 1, creditNotes: 1)).Should().Equal("CreditNote", "Refund");
        TravelInvoiceStateMachine.AllowedActions(State("Void", 100)).Should().BeEmpty();
        TravelInvoiceStateMachine.CanVoid(State("Issued", 100, creditNotes: 1)).Should().BeFalse();
    }

    [Fact]
    public void Payment_actions_follow_status_and_unallocated_amount()
    {
        TravelFinanceRules.PaymentActions(new TravelPaymentState { Status = "Pending", Amount = 100, Method = "Cheque" })
            .Should().Equal("Clear", "Fail", "Allocate");
        TravelFinanceRules.PaymentActions(new TravelPaymentState { Status = "Completed", Amount = 100, Allocated = 60, Method = "BankTransfer" })
            .Should().Equal("Allocate", "Refund", "Reconcile", "Unallocate");
        TravelFinanceRules.PaymentActions(new TravelPaymentState { Status = "Completed", Amount = 100, Allocated = 100, Method = "Cash" })
            .Should().Equal("Unallocate");
        TravelFinanceRules.PaymentActions(new TravelPaymentState { Status = "Completed", Amount = 100, Allocated = 100, Method = "Card", ReconciledAt = Today })
            .Should().BeEmpty();
    }

    // ── Planner ────────────────────────────────────────────────────────────

    private static TravelBillableItemDto Item(string type, int id, decimal amount, string currency = "PKR", string context = "Booking", int contextId = 70) => new()
    {
        SourceType = type, SourceId = id, Amount = amount, CurrencyCode = currency, Description = $"{type} {id}", ContextType = context, ContextId = contextId, CustomerId = 5
    };

    [Fact]
    public void Planner_builds_lines_totals_and_a_single_context()
    {
        var billable = new[] { Item("Flight", 1, 160_000), Item("Hotel", 2, 380_000) };
        var (plan, error) = TravelInvoicePlanner.Build(
            [new("", 1, 160_000, "Flight", 1), new("Hilton Makkah", 1, 375_000, "Hotel", 2), new("Insurance", 2, 4_000, null, null)], 9_000, billable, "PKR");
        error.Should().BeNull();
        plan!.Lines.Select(l => l.Description).Should().Equal("Flight 1", "Hilton Makkah", "Insurance");
        plan.Subtotal.Should().Be(543_000);
        plan.Total.Should().Be(534_000);
        plan.BookingId.Should().Be(70);
        plan.VisaCaseId.Should().BeNull();
    }

    [Fact]
    public void Planner_refuses_double_billing_foreign_items_and_currency_mismatch()
    {
        var billable = new[] { Item("Flight", 1, 160_000), Item("Hotel", 2, 4_000, "SAR"), Item("VisaServiceFee", 3, 10_000, context: "VisaCase", contextId: 9) };
        TravelInvoicePlanner.Build([new("", 1, 1, "Flight", 1), new("", 1, 1, "Flight", 1)], 0, billable, "PKR").Error.Should().Contain("listed twice");
        TravelInvoicePlanner.Build([new("", 1, 1, "Flight", 99)], 0, billable, "PKR").Error.Should().Contain("already invoiced");
        TravelInvoicePlanner.Build([new("", 1, 1, "Hotel", 2)], 0, billable, "PKR").Error.Should().Contain("priced in SAR");
        TravelInvoicePlanner.Build([new("", 2, 1, "Flight", 1)], 0, billable, "PKR").Error.Should().Contain("quantity 1");
        TravelInvoicePlanner.Build([new("  ", 1, 10, null, null)], 0, billable, "PKR").Error.Should().Contain("description is required");
        TravelInvoicePlanner.Build([new("Fee", 1, 10, null, null)], 11, billable, "PKR").Error.Should().Contain("discount");
        TravelInvoicePlanner.Build([new("", 1, 1, "Bogus", 1)], 0, billable, "PKR").Error.Should().Contain("unknown source");
        TravelInvoicePlanner.Build([], 0, billable, "PKR").Error.Should().Contain("at least one line");

        var (mixed, _) = TravelInvoicePlanner.Build([new("", 1, 1, "Flight", 1), new("", 1, 1, "VisaServiceFee", 3)], 0, billable, "PKR");
        mixed!.BookingId.Should().BeNull();
        mixed.VisaCaseId.Should().BeNull();
    }

    // ── Statement and summary ──────────────────────────────────────────────

    private static TravelStatementMovement Move(DateTime date, string type, decimal debit, decimal credit, int order)
        => new(date, type, $"{type}-{date:MMdd}", type, debit, credit, null, null, order);

    [Fact]
    public void Statement_has_an_opening_balance_and_running_totals()
    {
        var movements = new[]
        {
            Move(new DateTime(2027, 1, 5), "Invoice", 160_000, 0, 1),
            Move(new DateTime(2027, 1, 10), "Payment", 0, 100_000, 3),
            Move(new DateTime(2027, 2, 3), "CreditNote", 0, 20_000, 2),
            Move(new DateTime(2027, 2, 3), "Invoice", 50_000, 0, 1),
            Move(new DateTime(2027, 3, 1), "Refund", 5_000, 0, 4)
        };
        var (opening, entries, closing) = TravelFinanceRules.Statement(movements, new DateTime(2027, 2, 1), new DateTime(2027, 2, 28));
        opening.Should().Be(60_000);
        entries.Select(e => e.EntryType).Should().Equal("Invoice", "CreditNote");
        entries.Select(e => e.Balance).Should().Equal(110_000, 90_000);
        closing.Should().Be(90_000);

        var all = TravelFinanceRules.Statement(movements, null, null);
        all.Opening.Should().Be(0);
        all.Closing.Should().Be(95_000);

        var inverted = TravelFinanceRules.Statement(movements, new DateTime(2027, 3, 1), new DateTime(2027, 2, 1));
        inverted.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Summary_counts_are_zero_filled_and_sum_to_the_total()
    {
        var s = GetTravelFinanceSummaryQueryHandler.Build(
            [new TravelFinanceCurrencySummaryDto { CurrencyCode = "SAR" }, new TravelFinanceCurrencySummaryDto { CurrencyCode = "PKR" }],
            new Dictionary<string, int> { ["Issued"] = 7, ["Draft"] = 2 });
        s.InvoiceCounts.Keys.Should().BeEquivalentTo(TravelValues.InvoiceStatuses);
        s.InvoiceCounts["Void"].Should().Be(0);
        s.InvoiceTotal.Should().Be(s.InvoiceCounts.Values.Sum()).And.Be(9);
        s.Currencies.Select(c => c.CurrencyCode).Should().Equal("PKR", "SAR");
    }

    [Fact]
    public void Decorate_fills_payment_status_overdue_and_schedule()
    {
        var invoice = new TravelInvoiceDto { Status = "Issued", Total = 160_000, Paid = 100_000, DueDate = new DateTime(2027, 2, 15) };
        TravelFinanceRules.Decorate(invoice, [Inst(1, new DateTime(2027, 1, 15), 100_000), Inst(2, new DateTime(2027, 1, 25), 60_000)], Today);
        invoice.PaymentStatus.Should().Be("PartiallyPaid");
        invoice.OverdueAmount.Should().Be(60_000);
        invoice.NextDueDate.Should().Be(new DateTime(2027, 1, 25));
        invoice.Installments.Select(i => i.Status).Should().Equal("Paid", "Overdue");

        var draft = new TravelInvoiceListItemDto { Status = "Draft", Total = 50, DueDate = new DateTime(2026, 1, 1) };
        TravelFinanceRules.Decorate(draft, [], Today);
        draft.OverdueAmount.Should().Be(0);
        draft.PaymentStatus.Should().BeNull();
    }
}
