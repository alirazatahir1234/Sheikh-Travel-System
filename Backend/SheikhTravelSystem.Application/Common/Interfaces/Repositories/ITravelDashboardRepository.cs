using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Dashboard.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>Dashboard counts and trends the existing summaries do not provide. Every statement filters on TenantId; lists apply the branch clamp.</summary>
public interface ITravelDashboardRepository
{
    /// <summary>Issued invoices with an overdue amount as of <paramref name="today"/>: count and the overdue amount per currency.</summary>
    Task<(int Count, IReadOnlyList<TravelCurrencyAmountDto> Amounts)> GetOverdueInvoicesAsync(int tenantId, TravelBranchScope scope, DateTime today,
        CancellationToken cancellationToken = default);
    Task<int> CountOverdueSupplierBillsAsync(int tenantId, TravelBranchScope scope, DateTime today, CancellationToken cancellationToken = default);
    /// <summary>Enquiries created per ISO week starting at <paramref name="fromUtc"/> (weeks with none are filled by the caller).</summary>
    Task<IReadOnlyList<TravelTrendPointDto>> GetEnquiryTrendAsync(int tenantId, TravelBranchScope scope, DateTime fromUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelMoneyTrendPointDto>> GetInvoicingTrendAsync(int tenantId, TravelBranchScope scope, DateTime fromUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelDecisionTrendPointDto>> GetVisaDecisionTrendAsync(int tenantId, TravelBranchScope scope, DateTime fromUtc, CancellationToken cancellationToken = default);
}
