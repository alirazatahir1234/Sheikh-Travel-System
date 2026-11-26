using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Profitability;

/// <summary>
/// Agent commission, derived from rules and never stored. Each issued invoice may pay a staff agent and a sub-agent. Per line,
/// the most specific rule in force on the issue date applies (the person's own rule over the default; a scope rule over "All").
/// %-of-sell uses the line's net revenue in invoice currency; %-of-margin uses its margin in reporting currency (floored at 0
/// per invoice and rule). Commission is Accrued while the customer owes on the invoice and Payable once it is settled.
/// </summary>
public static class TravelCommissionEngine
{
    public static string ScopeOf(string? sourceType) => sourceType switch
    {
        "Flight" => "Flight",
        "Hotel" => "Hotel",
        "VisaServiceFee" or "VisaEmbassyFee" => "Visa",
        "GroupMember" => "Umrah",
        _ => "All"
    };

    public static TravelCommissionRuleDto? PickRule(IEnumerable<TravelCommissionRuleDto> rules, string beneficiaryType, int beneficiaryId, string scope, DateTime on)
        => rules.Where(r => r.IsActive && r.BeneficiaryType == beneficiaryType
                            && (beneficiaryType == "Staff" ? r.UserId is null || r.UserId == beneficiaryId : r.SubAgentId is null || r.SubAgentId == beneficiaryId)
                            && (r.Scope == scope || r.Scope == "All")
                            && r.ValidFrom.Date <= on.Date && (r.ValidTo is null || r.ValidTo.Value.Date >= on.Date))
            .OrderByDescending(r => (beneficiaryType == "Staff" ? r.UserId : r.SubAgentId) is not null)
            .ThenByDescending(r => r.Scope != "All")
            .ThenByDescending(r => r.ValidFrom).ThenByDescending(r => r.Id)
            .FirstOrDefault();

    public sealed record Result(IReadOnlyList<TravelCommissionLineDto> Lines, IReadOnlyDictionary<string, decimal> CommissionByContext);

    public static Result Compute(IReadOnlyList<TravelAttributedInvoiceFact> invoices, IReadOnlyList<TravelProfitabilityEngine.LineResult> lines,
        IReadOnlyList<TravelCommissionRuleDto> rules, TravelFx fx)
    {
        var linesByInvoice = lines.GroupBy(l => l.InvoiceId).ToDictionary(g => g.Key, g => g.ToList());
        var output = new List<TravelCommissionLineDto>();
        var byContext = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var inv in invoices)
        {
            var invoiceLines = linesByInvoice.GetValueOrDefault(inv.InvoiceId, []);
            if (invoiceLines.Count == 0) continue;
            var beneficiaries = new List<(string Type, int Id, string Name)>();
            if (inv.AgentUserId is int u) beneficiaries.Add(("Staff", u, inv.AgentName ?? $"User #{u}"));
            if (inv.SubAgentId is int s) beneficiaries.Add(("SubAgent", s, inv.SubAgentName ?? $"Sub-agent #{s}"));

            foreach (var (type, id, name) in beneficiaries)
            {
                var groups = invoiceLines
                    .Select(l => (Line: l, Rule: PickRule(rules, type, id, ScopeOf(l.SourceType), inv.IssueDate)))
                    .Where(x => x.Rule is not null)
                    .GroupBy(x => x.Rule!.Id);
                foreach (var g in groups)
                {
                    var rule = g.First().Rule!;
                    var members = g.Select(x => x.Line).ToList();
                    var marginBasis = rule.Basis == "PercentOfMargin";
                    var missing = marginBasis && members.Any(m => m.Margin is null);
                    var basis = marginBasis ? members.Sum(m => m.Margin ?? 0) : members.Sum(m => m.NetInInvoiceCurrency);
                    var amount = missing ? 0 : Math.Round(Math.Max(0, basis) * rule.Rate / 100m, 2, MidpointRounding.AwayFromZero);
                    output.Add(new TravelCommissionLineDto
                    {
                        InvoiceId = inv.InvoiceId, InvoiceNumber = inv.InvoiceNumber, IssueDate = inv.IssueDate, CustomerId = inv.CustomerId,
                        CustomerName = inv.CustomerName, BeneficiaryType = type, BeneficiaryId = id, BeneficiaryName = name,
                        CurrencyCode = marginBasis ? fx.ReportingCurrency : inv.CurrencyCode, BasisAmount = Math.Round(basis, 2, MidpointRounding.AwayFromZero),
                        Basis = rule.Basis, Rate = rule.Rate, Amount = amount, Status = inv.Balance <= 0 ? "Payable" : "Accrued", MissingRate = missing
                    });
                    if (amount == 0) continue;

                    // Spread over the lines' contexts for profitability (reporting currency).
                    var reporting = marginBasis ? amount : fx.Convert(amount, inv.CurrencyCode, inv.IssueDate);
                    if (reporting is null) continue;
                    var weights = members.Select(m => marginBasis ? m.Margin ?? 0 : m.NetInInvoiceCurrency).ToList();
                    var totalWeight = weights.Sum();
                    for (var i = 0; i < members.Count; i++)
                    {
                        var share = totalWeight == 0 ? 1m / members.Count : weights[i] / totalWeight;
                        byContext[members[i].ContextKey] = byContext.GetValueOrDefault(members[i].ContextKey) + reporting.Value * share;
                    }
                }
            }
        }
        return new Result(output, byContext);
    }

    /// <summary>Accrued / payable / paid per beneficiary and currency.</summary>
    public static IReadOnlyList<TravelCommissionBalanceDto> Balances(IEnumerable<TravelCommissionLineDto> lines, IEnumerable<TravelCommissionPayoutDto> payouts)
    {
        var map = new Dictionary<(string, int, string), TravelCommissionBalanceDto>();
        TravelCommissionBalanceDto Row(string type, int id, string name, string currency)
        {
            if (!map.TryGetValue((type, id, currency), out var row))
                map[(type, id, currency)] = row = new TravelCommissionBalanceDto { BeneficiaryType = type, BeneficiaryId = id, BeneficiaryName = name, CurrencyCode = currency };
            return row;
        }
        foreach (var l in lines.Where(l => !l.MissingRate))
        {
            var row = Row(l.BeneficiaryType, l.BeneficiaryId, l.BeneficiaryName, l.CurrencyCode);
            if (l.Status == "Payable") row.Payable += l.Amount; else row.Accrued += l.Amount;
        }
        foreach (var p in payouts) Row(p.BeneficiaryType, p.BeneficiaryId, p.BeneficiaryName, p.CurrencyCode).Paid += p.Amount;
        return map.Values.OrderBy(r => r.BeneficiaryName).ThenBy(r => r.CurrencyCode, StringComparer.Ordinal).ToList();
    }
}
