using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>Raw facts for the profitability engine. Every statement filters on TenantId; contexts apply the branch clamp.</summary>
public interface ITravelProfitabilityRepository
{
    /// <summary>Booking files (customer-owned), visa cases and groups whose travel date falls in the range.</summary>
    Task<IReadOnlyList<TravelContextFact>> GetContextsAsync(int tenantId, TravelBranchScope scope, IReadOnlyList<string> contextTypes,
        DateTime? travelFrom, DateTime? travelTo, CancellationToken cancellationToken = default);
    /// <summary>The contexts with the given keys ("Booking:12"), regardless of dates.</summary>
    Task<IReadOnlyList<TravelContextFact>> GetContextsByKeyAsync(int tenantId, IReadOnlyList<string> keys, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelRevenueFact>> GetRevenueFactsAsync(int tenantId, IReadOnlyList<string> keys, CancellationToken cancellationToken = default);
    /// <summary>Contexts of the given invoices (to compute their margins).</summary>
    Task<IReadOnlyList<string>> GetInvoiceContextKeysAsync(int tenantId, IReadOnlyList<int> invoiceIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelInvoiceCreditFact>> GetInvoiceCreditsAsync(int tenantId, IReadOnlyList<int> invoiceIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelCostFact>> GetCostFactsAsync(int tenantId, IReadOnlyList<string> keys, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelBillCreditFact>> GetBillCreditsAsync(int tenantId, IReadOnlyList<int> billIds, CancellationToken cancellationToken = default);
}
