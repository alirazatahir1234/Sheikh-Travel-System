using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Finance;

/// <summary>
/// Invoice lifecycle: Draft → Issued (immutable; corrections are credit notes) → Void (only with no money movements).
/// Drafts can be deleted. Money actions on issued invoices depend on the balance.
/// </summary>
public static class TravelInvoiceStateMachine
{
    public static class Actions
    {
        public const string Edit = "Edit";
        public const string Issue = "Issue";
        public const string Delete = "Delete";
        public const string Void = "Void";
        public const string RecordPayment = "RecordPayment";
        public const string CreditNote = "CreditNote";
        public const string Refund = "Refund";
    }

    public static IReadOnlyList<string> AllowedActions(TravelInvoiceState s)
    {
        if (s.Status == "Draft") return [Actions.Edit, Actions.Issue, Actions.Delete];
        if (s.Status != "Issued") return [];
        var actions = new List<string>();
        if (s.Balance - s.PendingPaid > 0) actions.Add(Actions.RecordPayment);
        if (s.Total - s.Credited > 0) actions.Add(Actions.CreditNote);
        if (s.Balance < 0) actions.Add(Actions.Refund);
        if (CanVoid(s)) actions.Add(Actions.Void);
        return actions;
    }

    /// <summary>Void only when nothing has been paid (even pending), credited or refunded.</summary>
    public static bool CanVoid(TravelInvoiceState s) => s.Status == "Issued" && s.AllocationCount == 0 && s.CreditNoteCount == 0 && s.RefundCount == 0;
}

public static class TravelFinanceRules
{
    /// <summary>Money is rounded to 2 decimals, half away from zero.</summary>
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal LineTotal(decimal quantity, decimal unitPrice) => Round(quantity * unitPrice);

    /// <summary>Invoice currency amount for a payment amount at a recorded rate (invoice units per payment unit).</summary>
    public static decimal Convert(decimal amountFromPayment, decimal rate) => Round(amountFromPayment * rate);

    public static string? PaymentStatus(string invoiceStatus, TravelInvoiceMoney m)
    {
        if (invoiceStatus != "Issued") return null;
        if (m.Balance < 0) return "CreditDue";
        if (m.Balance == 0) return "Paid";
        return m.Paid - m.Refunded + m.Credited > 0 ? "PartiallyPaid" : "Unpaid";
    }

    /// <summary>
    /// What is covered: Completed payments net of refunds, plus credit notes. Installments (or, without a schedule, the whole
    /// invoice on its due date) are covered oldest-due first.
    /// </summary>
    public static decimal Covered(TravelInvoiceMoney m) => m.Paid - m.Refunded + m.Credited;

    public static IReadOnlyList<TravelInstallmentDto> CoverInstallments(IReadOnlyList<TravelInstallmentDto> installments, decimal covered, DateTime today)
    {
        var remaining = Math.Max(0, covered);
        var result = new List<TravelInstallmentDto>();
        foreach (var i in installments.OrderBy(i => i.DueDate).ThenBy(i => i.Sequence))
        {
            var take = Math.Min(remaining, i.Amount);
            remaining -= take;
            result.Add(new TravelInstallmentDto
            {
                InvoiceId = i.InvoiceId, Sequence = i.Sequence, DueDate = i.DueDate, Amount = i.Amount, Covered = take,
                Status = take >= i.Amount ? "Paid" : i.DueDate.Date < today.Date ? "Overdue" : take > 0 ? "PartlyPaid" : "Due"
            });
        }
        return result;
    }

    /// <summary>Amount past due as of <paramref name="today"/>: what fell due before today minus what is covered (never negative).</summary>
    public static decimal OverdueAmount(IReadOnlyList<TravelInstallmentDto> installments, DateTime? dueDate, decimal total, decimal covered, DateTime today)
    {
        var dueBefore = installments.Count > 0
            ? installments.Where(i => i.DueDate.Date < today.Date).Sum(i => i.Amount)
            : dueDate is DateTime d && d.Date < today.Date ? total : 0;
        return Math.Max(0, dueBefore - Math.Max(0, covered));
    }

    /// <summary>Earliest due date not yet fully covered.</summary>
    public static DateTime? NextDueDate(IReadOnlyList<TravelInstallmentDto> covered, DateTime? dueDate, decimal balance)
    {
        if (balance <= 0) return null;
        if (covered.Count == 0) return dueDate;
        return covered.Where(i => i.Status != "Paid").OrderBy(i => i.DueDate).Select(i => (DateTime?)i.DueDate).FirstOrDefault();
    }

    /// <summary>Fills payment status, installment coverage, overdue amount and next due date on a list row or detail.</summary>
    public static void Decorate(TravelInvoiceListItemDto invoice, IReadOnlyList<TravelInstallmentDto> installments, DateTime today)
    {
        invoice.PaymentStatus = PaymentStatus(invoice.Status, invoice);
        if (invoice.Status != "Issued")
        {
            invoice.OverdueAmount = 0;
            invoice.NextDueDate = null;
            return;
        }
        var covered = Covered(invoice);
        var schedule = CoverInstallments(installments, covered, today);
        invoice.OverdueAmount = OverdueAmount(installments, invoice.DueDate, invoice.Total, covered, today);
        invoice.NextDueDate = NextDueDate(schedule, invoice.DueDate, invoice.Balance);
        if (invoice is TravelInvoiceDto detail) detail.Installments = schedule;
    }

    /// <summary>Installments, when given, must be positive, on or after the issue date, and add up to the total.</summary>
    public static string? ValidateInstallments(IReadOnlyList<SaveTravelInstallmentDto> installments, decimal total, DateTime? issueDate)
    {
        if (installments.Count == 0) return null;
        if (installments.Any(i => i.Amount <= 0)) return "Every installment needs a positive amount.";
        if (issueDate is DateTime d && installments.Any(i => i.DueDate.Date < d.Date)) return "Installments cannot fall before the issue date.";
        if (installments.Select(i => i.DueDate.Date).Distinct().Count() != installments.Count) return "Two installments cannot share a due date.";
        var sum = installments.Sum(i => Round(i.Amount));
        return sum == total ? null : $"Installments add up to {sum:0.##} but the invoice total is {total:0.##}.";
    }

    /// <summary>
    /// Converts and checks allocations for one payment. <paramref name="available"/> = per invoice, what may still be applied
    /// (balance minus amounts reserved by pending payments), in invoice currency.
    /// </summary>
    public static (IReadOnlyList<TravelAllocationWrite> Writes, string? Error) PlanAllocations(
        IReadOnlyList<TravelAllocationInputDto> inputs, string paymentCurrency, decimal paymentUnallocated,
        IReadOnlyDictionary<int, (string Currency, decimal Available)> invoices)
    {
        if (inputs.Select(i => i.InvoiceId).Distinct().Count() != inputs.Count) return ([], "Each invoice once per payment.");
        var writes = new List<TravelAllocationWrite>();
        foreach (var a in inputs)
        {
            if (!invoices.TryGetValue(a.InvoiceId, out var inv)) return ([], "An invoice to pay is not an open invoice of this customer.");
            if (a.AmountFromPayment <= 0) return ([], "Allocated amounts must be positive.");
            decimal rate;
            if (inv.Currency == paymentCurrency)
            {
                if (a.ExchangeRate is decimal r && r != 1) return ([], "Same-currency allocations use a rate of 1.");
                rate = 1;
            }
            else
            {
                if (a.ExchangeRate is not decimal r || r <= 0)
                    return ([], $"Enter the exchange rate from {paymentCurrency} to {inv.Currency}.");
                rate = r;
            }
            var applied = Convert(a.AmountFromPayment, rate);
            if (applied <= 0) return ([], "The converted amount rounds to zero.");
            if (applied > inv.Available) return ([], $"{applied:0.##} {inv.Currency} is more than the invoice's open balance ({inv.Available:0.##} {inv.Currency}).");
            writes.Add(new TravelAllocationWrite(a.InvoiceId, Round(a.AmountFromPayment), rate, applied));
        }
        var used = writes.Sum(w => w.AmountFromPayment);
        if (used > paymentUnallocated) return ([], $"Allocations ({used:0.##} {paymentCurrency}) exceed what is left on the payment ({paymentUnallocated:0.##} {paymentCurrency}).");
        return (writes, null);
    }

    /// <summary>What can still be applied to an issued invoice: balance less amounts reserved by pending payments.</summary>
    public static decimal Available(TravelInvoiceMoney m) => Math.Max(0, m.Balance - m.PendingPaid);

    /// <summary>Running balance per currency: opening = movements before <paramref name="from"/>; entries within the range.</summary>
    public static (decimal Opening, IReadOnlyList<TravelStatementEntryDto> Entries, decimal Closing) Statement(
        IEnumerable<TravelStatementMovement> movements, DateTime? from, DateTime? to)
    {
        var ordered = movements.OrderBy(m => m.Date).ThenBy(m => m.Order).ThenBy(m => m.Reference, StringComparer.Ordinal).ToList();
        var opening = ordered.Where(m => from.HasValue && m.Date.Date < from.Value.Date).Sum(m => m.Debit - m.Credit);
        var running = opening;
        var entries = new List<TravelStatementEntryDto>();
        foreach (var m in ordered.Where(m => (!from.HasValue || m.Date.Date >= from.Value.Date) && (!to.HasValue || m.Date.Date <= to.Value.Date)))
        {
            running += m.Debit - m.Credit;
            entries.Add(new TravelStatementEntryDto
            {
                Date = m.Date, EntryType = m.EntryType, Reference = m.Reference, Description = m.Description, Debit = m.Debit, Credit = m.Credit,
                Balance = running, InvoiceId = m.InvoiceId, PaymentId = m.PaymentId
            });
        }
        return (opening, entries, running);
    }

    /// <summary>Payment actions: clear / fail a pending payment, allocate, refund an advance, reconcile.</summary>
    public static IReadOnlyList<string> PaymentActions(TravelPaymentState p)
    {
        var actions = new List<string>();
        if (p.Status == "Pending") actions.AddRange(["Clear", "Fail"]);
        if (p.Status is "Pending" or "Completed" && p.Unallocated > 0) actions.Add("Allocate");
        if (p.Status == "Completed" && p.Unallocated > 0) actions.Add("Refund");
        if (p.Status == "Completed" && p.ReconciledAt is null && p.Method != "Cash") actions.Add("Reconcile");
        if (p.Status is "Pending" or "Completed" or "Failed" && p.Allocated > 0 && p.ReconciledAt is null) actions.Add("Unallocate");
        return actions;
    }
}
