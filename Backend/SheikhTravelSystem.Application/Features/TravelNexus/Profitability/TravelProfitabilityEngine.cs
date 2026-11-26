using SheikhTravelSystem.Application.Features.TravelNexus.Profitability.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Profitability;

/// <summary>
/// Pure profitability calculation per context (booking file, visa case, group) in the reporting currency.
/// <list type="bullet">
/// <item>Revenue: issued invoice lines; invoice discount and credit notes are spread over the lines pro-rata.</item>
/// <item>Cost: posted bill lines net of supplier commission (supplier credits spread pro-rata over the bill's lines), plus
/// estimates for services that are not billed yet.</item>
/// <item>Each line's margin = its revenue − context cost × its share of context revenue, so line margins add up to the context.</item>
/// </list>
/// Commission is added by <see cref="TravelCommissionEngine"/> from the line results.
/// </summary>
public static class TravelProfitabilityEngine
{
    public static string Key(string type, int id) => $"{type}:{id}";

    /// <summary>One invoice line after discount and credits, with its share of the context's cost.</summary>
    public sealed record LineResult(int InvoiceId, string? SourceType, string ContextKey, string InvoiceCurrency, DateTime IssueDate,
        decimal NetInInvoiceCurrency, decimal? Revenue, decimal? Margin);

    public sealed class ContextResult
    {
        public required TravelContextFact Context { get; init; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal Commission { get; set; }
        public int MissingRateCount { get; set; }
        public List<TravelProfitLineDto> Lines { get; } = [];
    }

    public sealed record Result(IReadOnlyDictionary<string, ContextResult> Contexts, IReadOnlyList<LineResult> Lines);

    public static Result Compute(IReadOnlyList<TravelContextFact> contexts, IReadOnlyList<TravelRevenueFact> revenue,
        IReadOnlyList<TravelInvoiceCreditFact> invoiceCredits, IReadOnlyList<TravelCostFact> costs, IReadOnlyList<TravelBillCreditFact> billCredits,
        TravelFx fx)
    {
        var results = contexts.ToDictionary(c => Key(c.ContextType, c.ContextId), c => new ContextResult { Context = c });
        var creditsByInvoice = invoiceCredits.GroupBy(c => c.InvoiceId).ToDictionary(g => g.Key, g => g.ToList());

        // ── Revenue per line ──
        var pending = new List<(TravelRevenueFact Fact, string Key, decimal Net, decimal? Converted)>();
        foreach (var r in revenue)
        {
            var key = Key(r.ContextType, r.ContextId);
            if (!results.TryGetValue(key, out var ctx)) continue;
            var invoiceTotal = r.InvoiceSubtotal - r.InvoiceDiscount;
            var afterDiscount = r.InvoiceSubtotal == 0 ? 0 : r.LineTotal * invoiceTotal / r.InvoiceSubtotal;
            var share = invoiceTotal == 0 ? 0 : afterDiscount / invoiceTotal;
            decimal? converted = fx.Convert(afterDiscount, r.CurrencyCode, r.IssueDate);
            var missing = converted is null;
            var net = afterDiscount;
            foreach (var c in creditsByInvoice.GetValueOrDefault(r.InvoiceId, []))
            {
                var part = c.Amount * share;
                net -= part;
                var convertedCredit = fx.Convert(part, r.CurrencyCode, c.IssuedOn);
                if (convertedCredit is null) missing = true;
                else if (converted is not null) converted -= convertedCredit;
            }
            if (missing) { ctx.MissingRateCount++; converted = null; }
            ctx.Lines.Add(new TravelProfitLineDto
            {
                Kind = "Revenue", Description = r.Description, Reference = r.InvoiceNumber ?? $"#{r.InvoiceId}", Date = r.IssueDate,
                Amount = Round(net), CurrencyCode = r.CurrencyCode, Converted = converted is null ? null : Round(converted.Value)
            });
            if (converted is not null) ctx.Revenue += converted.Value;
            pending.Add((r, key, net, converted));
        }

        // ── Cost: bill lines (credits spread pro-rata), estimates ──
        var creditByBill = billCredits.GroupBy(c => c.BillId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var c in costs)
        {
            var key = Key(c.ContextType, c.ContextId);
            if (!results.TryGetValue(key, out var ctx)) continue;
            decimal? converted = fx.Convert(c.Amount, c.CurrencyCode, c.Date);
            var amount = c.Amount;
            var missing = converted is null;
            if (c.BillId is int billId)
            {
                foreach (var credit in creditByBill.GetValueOrDefault(billId, []))
                {
                    if (credit.BillNet == 0) continue;
                    var part = credit.Amount * c.Amount / credit.BillNet;
                    amount -= part;
                    var convertedCredit = fx.Convert(part, credit.CurrencyCode, credit.IssuedOn);
                    if (convertedCredit is null) missing = true;
                    else if (converted is not null) converted -= convertedCredit;
                }
            }
            if (missing) { ctx.MissingRateCount++; converted = null; }
            ctx.Lines.Add(new TravelProfitLineDto
            {
                Kind = "Cost", Description = c.Description, Reference = c.Reference, Date = c.Date, Amount = Round(amount), CurrencyCode = c.CurrencyCode,
                Converted = converted is null ? null : Round(converted.Value), Estimated = c.Estimated
            });
            if (converted is null) continue;
            ctx.Cost += converted.Value;
            if (c.Estimated) ctx.EstimatedCost += converted.Value;
        }

        // ── Line margins ──
        var lines = new List<LineResult>();
        foreach (var (fact, key, net, converted) in pending)
        {
            var ctx = results[key];
            decimal? margin = null;
            if (converted is not null)
                margin = converted.Value - (ctx.Revenue == 0 ? 0 : ctx.Cost * converted.Value / ctx.Revenue);
            lines.Add(new LineResult(fact.InvoiceId, fact.SourceType, key, fact.CurrencyCode, fact.IssueDate, net, converted, margin));
        }
        return new Result(results, lines);
    }

    public static TravelContextProfitDto ToDto(ContextResult r) => new()
    {
        ContextType = r.Context.ContextType, ContextId = r.Context.ContextId, Label = r.Context.Label, TravelDate = r.Context.TravelDate,
        BranchId = r.Context.BranchId, BranchName = r.Context.BranchName, AgentUserId = r.Context.AgentUserId, AgentName = r.Context.AgentName,
        Revenue = Round(r.Revenue), Cost = Round(r.Cost), EstimatedCost = Round(r.EstimatedCost), Commission = Round(r.Commission),
        MissingRateCount = r.MissingRateCount
    };

    public static TravelProfitGroupDto Sum(string key, string label, IEnumerable<TravelContextProfitDto> rows)
    {
        var list = rows.ToList();
        return new TravelProfitGroupDto
        {
            Key = key, Label = label, Contexts = list.Count, Revenue = list.Sum(r => r.Revenue), Cost = list.Sum(r => r.Cost),
            EstimatedCost = list.Sum(r => r.EstimatedCost), Commission = list.Sum(r => r.Commission)
        };
    }

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
