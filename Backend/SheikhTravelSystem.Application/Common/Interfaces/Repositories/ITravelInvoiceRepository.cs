using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Customer invoices, billable sources and finance reports. Every statement filters on TenantId; lists apply the branch clamp.
/// Money columns are computed from allocations, credit notes and refunds — never stored twice.
/// </summary>
public interface ITravelInvoiceRepository
{
    Task<TravelInvoiceState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<PagedResult<TravelInvoiceListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelInvoiceListFilter filter, int page, int pageSize,
        CancellationToken cancellationToken = default);
    Task<TravelInvoiceDto?> GetDetailAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelInstallmentDto>> GetInstallmentsAsync(IReadOnlyList<int> invoiceIds, int tenantId, CancellationToken cancellationToken = default);
    /// <summary>Issued invoices of the customer with a positive balance (for allocating a payment), oldest due first.</summary>
    Task<IReadOnlyList<TravelInvoiceListItemDto>> GetOpenInvoicesAsync(int tenantId, int customerId, CancellationToken cancellationToken = default);

    /// <summary>Sold items not on any live invoice (plus those already on <paramref name="includeInvoiceId"/>).</summary>
    Task<IReadOnlyList<TravelBillableItemDto>> GetBillableAsync(int tenantId, TravelBranchScope scope, int? customerId, string? currencyCode, int? includeInvoiceId,
        CancellationToken cancellationToken = default);

    Task<int> CreateAsync(int tenantId, TravelInvoiceHeaderWrite header, IReadOnlyList<TravelInvoiceLineWrite> lines,
        IReadOnlyList<SaveTravelInstallmentDto> installments, int? userId, CancellationToken cancellationToken = default);
    /// <summary>Replaces header, lines and installments of a Draft; false when it is no longer a draft.</summary>
    Task<bool> UpdateDraftAsync(int tenantId, int id, TravelInvoiceHeaderWrite header, IReadOnlyList<TravelInvoiceLineWrite> lines,
        IReadOnlyList<SaveTravelInstallmentDto> installments, int? userId, CancellationToken cancellationToken = default);
    /// <summary>Allocates the number and issues the Draft; null when it is no longer a draft.</summary>
    Task<string?> IssueAsync(int tenantId, int id, DateTime issueDate, DateTime dueDate, int? userId, DateTime utcNow, CancellationToken cancellationToken = default);
    /// <summary>Voids an issued invoice with no money movements (re-checked under lock) and releases its source lines.</summary>
    Task<bool> VoidAsync(int tenantId, int id, string reason, int? userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<bool> DeleteDraftAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    // ── Reports ──
    Task<IReadOnlyList<TravelFinanceCurrencySummaryDto>> GetCurrencySummaryAsync(int tenantId, TravelBranchScope scope, DateTime today,
        DateTime monthStart, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelCustomerBalanceDto>> GetCustomerBalancesAsync(int tenantId, TravelBranchScope scope, int? customerId, DateTime today,
        bool openOnly, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelStatementMovement>> GetStatementMovementsAsync(int tenantId, int customerId, string currencyCode,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCustomerCurrenciesAsync(int tenantId, int customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelChangedInvoiceLineDto>> GetChangedLinesAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelGroupFinanceRowDto>> GetGroupFinanceAsync(int tenantId, int groupId, CancellationToken cancellationToken = default);
}
