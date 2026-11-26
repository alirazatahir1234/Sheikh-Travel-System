using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance;

/// <summary>Supplier bills mirror customer invoices: Draft (editable) → Posted (immutable) → Void (only without money movements).</summary>
public static class TravelSupplierBillStateMachine
{
    public static IReadOnlyList<string> AllowedActions(TravelSupplierBillState s)
    {
        if (s.Status == "Draft") return ["Edit", "Post", "Delete"];
        if (s.Status != "Posted") return [];
        var actions = new List<string>();
        if (s.Balance - s.PendingPaid > 0) actions.Add("RecordPayment");
        if (s.Total - s.Credited > 0) actions.Add("Credit");
        if (s.Balance < 0) actions.Add("Refund");
        if (CanVoid(s)) actions.Add("Void");
        return actions;
    }

    public static bool CanVoid(TravelSupplierBillState s) => s.Status == "Posted" && s.AllocationCount == 0 && s.CreditCount == 0 && s.RefundCount == 0;
}

public static class TravelSupplierFinanceRules
{
    /// <summary>
    /// Commission on a bill line from the supplier's rule in force on the bill date: % of cost, or a fixed amount per unit
    /// (passenger / room-night) in the bill currency. Never above the line amount.
    /// </summary>
    public static decimal SuggestCommission(IReadOnlyList<TravelSupplierCommissionRuleDto> rules, string serviceType, decimal amount, int units,
        string billCurrency, DateTime billDate)
    {
        var rule = rules.Where(r => r.IsActive && r.ServiceType == serviceType && r.ValidFrom.Date <= billDate.Date && (r.ValidTo is null || r.ValidTo.Value.Date >= billDate.Date))
            .OrderByDescending(r => r.ValidFrom).ThenByDescending(r => r.Id).FirstOrDefault();
        if (rule is null) return 0;
        var commission = rule.Basis switch
        {
            "PercentOfCost" => amount * rule.Rate / 100m,
            "FixedPerUnit" when rule.CurrencyCode == billCurrency => rule.Rate * Math.Max(0, units),
            _ => 0m
        };
        return Math.Min(amount, TravelFinanceRules.Round(commission));
    }

    public sealed record BillPlan(IReadOnlyList<TravelSupplierBillLineWrite> Lines, decimal Gross, decimal Commission, decimal Net);

    /// <summary>Sourced lines must be unbilled services of the supplier in the bill currency; manual lines need a description.</summary>
    public static (BillPlan? Plan, string? Error) Build(IReadOnlyList<SaveTravelSupplierBillLineDto> lines, IReadOnlyList<TravelBillableCostDto> billable,
        string currencyCode)
    {
        if (lines.Count == 0) return (null, "Add at least one line.");
        var sources = billable.ToDictionary(b => (b.SourceType, b.SourceId));
        var seen = new HashSet<(string, int)>();
        var writes = new List<TravelSupplierBillLineWrite>();
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            var description = l.Description?.Trim() ?? string.Empty;
            string? contextType = null;
            int? contextId = null;
            if (l.SourceType is not null || l.SourceId is not null)
            {
                if (l.SourceType is null || l.SourceId is not int sourceId || !TravelValues.IsOneOf(l.SourceType, TravelValues.SupplierBillSourceTypes))
                    return (null, $"Line {i + 1}: unknown source.");
                if (!seen.Add((l.SourceType, sourceId))) return (null, $"Line {i + 1}: the same service is listed twice.");
                if (!sources.TryGetValue((l.SourceType, sourceId), out var item))
                    return (null, $"Line {i + 1}: the service is already billed, belongs to another supplier, or has no cost.");
                if (item.CurrencyCode != currencyCode) return (null, $"Line {i + 1}: the service cost is in {item.CurrencyCode}; this bill is in {currencyCode}.");
                if (description.Length == 0) description = item.Description;
            }
            else if (l.ContextType is not null || l.ContextId is not null)
            {
                if (!TravelValues.IsOneOf(l.ContextType, TravelValues.ContextTypes) || l.ContextId is not > 0) return (null, $"Line {i + 1}: unknown booking, visa case or group.");
                contextType = l.ContextType;
                contextId = l.ContextId;
            }
            if (description.Length == 0) return (null, $"Line {i + 1}: a description is required.");
            var amount = TravelFinanceRules.Round(l.Amount);
            var commission = TravelFinanceRules.Round(l.CommissionAmount);
            if (amount < 0 || commission < 0) return (null, $"Line {i + 1}: amounts cannot be negative.");
            if (commission > amount) return (null, $"Line {i + 1}: commission cannot exceed the line amount.");
            writes.Add(new TravelSupplierBillLineWrite(i + 1, description.Length > 300 ? description[..300] : description, l.SourceType, l.SourceId,
                contextType, contextId, amount, commission, amount - commission));
        }
        var gross = writes.Sum(w => w.Amount);
        var totalCommission = writes.Sum(w => w.CommissionAmount);
        return (new BillPlan(writes, gross, totalCommission, gross - totalCommission), null);
    }

    public static string? PaymentStatus(string status, TravelInvoiceMoney m) => TravelFinanceRules.PaymentStatus(status == "Posted" ? "Issued" : status, m);

    /// <summary>Supplier payment actions follow the customer-payment rules.</summary>
    public static IReadOnlyList<string> PaymentActions(string status, decimal amount, decimal allocated, decimal refunded, DateTime? reconciledAt, string method)
        => TravelFinanceRules.PaymentActions(new TravelPaymentState
        {
            Status = status, Amount = amount, Allocated = allocated, Refunded = refunded, ReconciledAt = reconciledAt, Method = method
        });
}
