using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Profitability;

/// <summary>Loads the facts and runs <see cref="TravelProfitabilityEngine"/> and <see cref="TravelCommissionEngine"/> for a tenant.</summary>
public sealed class TravelProfitabilityService(ITravelProfitabilityRepository facts, ITravelCommissionRepository commissions,
    ITravelSupplierSetupRepository setup, ITravelConfigRepository config)
{
    public sealed record Computation(TravelFx Fx, TravelProfitabilityEngine.Result Profit, TravelCommissionEngine.Result Commission);

    /// <summary>Reporting currency (settings, else the default currency) and its rate table; null when neither is set.</summary>
    public async Task<TravelFx?> FxAsync(int tenantId, CancellationToken ct)
    {
        var settings = await config.GetSettingsAsync(tenantId, ct);
        var reporting = settings.ReportingCurrencyCode ?? settings.DefaultCurrencyCode;
        if (string.IsNullOrWhiteSpace(reporting)) return null;
        var rates = await setup.GetExchangeRatesAsync(tenantId, ct);
        return new TravelFx(reporting, rates.Select(r => (r.CurrencyCode, r.EffectiveFrom, r.RateToReporting)));
    }

    /// <summary>Profit (and the commission it includes) for the given contexts.</summary>
    public async Task<Computation> ComputeAsync(int tenantId, TravelFx fx, IReadOnlyList<TravelContextFact> contexts, CancellationToken ct)
    {
        var keys = contexts.Select(c => TravelProfitabilityEngine.Key(c.ContextType, c.ContextId)).ToList();
        var revenue = await facts.GetRevenueFactsAsync(tenantId, keys, ct);
        var invoiceIds = revenue.Select(r => r.InvoiceId).Distinct().ToList();
        var credits = await facts.GetInvoiceCreditsAsync(tenantId, invoiceIds, ct);
        var costs = await facts.GetCostFactsAsync(tenantId, keys, ct);
        var billCredits = await facts.GetBillCreditsAsync(tenantId, costs.Where(c => c.BillId.HasValue).Select(c => c.BillId!.Value).Distinct().ToList(), ct);
        var profit = TravelProfitabilityEngine.Compute(contexts, revenue, credits, costs, billCredits, fx);

        var attributed = await commissions.GetAttributedInvoicesByIdsAsync(tenantId, invoiceIds, ct);
        var commission = TravelCommissionEngine.Compute(attributed, profit.Lines, await commissions.GetRulesAsync(tenantId, ct), fx);
        foreach (var (key, amount) in commission.CommissionByContext)
            if (profit.Contexts.TryGetValue(key, out var ctx)) ctx.Commission += amount;
        return new Computation(fx, profit, commission);
    }

    /// <summary>Commission lines for attributed invoices (optionally one beneficiary / issue date range).</summary>
    public async Task<(TravelFx Fx, IReadOnlyList<TravelCommissionLineDto> Lines)> CommissionLinesAsync(int tenantId, TravelFx fx, string? beneficiaryType,
        int? beneficiaryId, DateTime? issuedFrom, DateTime? issuedTo, CancellationToken ct)
    {
        var attributed = await commissions.GetAttributedInvoicesAsync(tenantId, beneficiaryType, beneficiaryId, issuedFrom, issuedTo, ct);
        if (attributed.Count == 0) return (fx, []);
        var keys = await facts.GetInvoiceContextKeysAsync(tenantId, attributed.Select(a => a.InvoiceId).ToList(), ct);
        var contexts = await facts.GetContextsByKeyAsync(tenantId, keys, ct);
        var revenue = (await facts.GetRevenueFactsAsync(tenantId, keys, ct));
        var credits = await facts.GetInvoiceCreditsAsync(tenantId, revenue.Select(r => r.InvoiceId).Distinct().ToList(), ct);
        var costs = await facts.GetCostFactsAsync(tenantId, keys, ct);
        var billCredits = await facts.GetBillCreditsAsync(tenantId, costs.Where(c => c.BillId.HasValue).Select(c => c.BillId!.Value).Distinct().ToList(), ct);
        var profit = TravelProfitabilityEngine.Compute(contexts, revenue, credits, costs, billCredits, fx);
        var result = TravelCommissionEngine.Compute(attributed, profit.Lines, await commissions.GetRulesAsync(tenantId, ct), fx);
        // An invoice can pay a staff agent and a sub-agent: keep only the beneficiary asked for.
        var lines = beneficiaryType is null ? result.Lines
            : result.Lines.Where(l => l.BeneficiaryType == beneficiaryType && (beneficiaryId is null || l.BeneficiaryId == beneficiaryId)).ToList();
        return (fx, lines);
    }
}
