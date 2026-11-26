using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>Supplier bills and payables reports. Every statement filters on TenantId; lists apply the branch clamp.</summary>
public interface ITravelSupplierBillRepository
{
    Task<TravelSupplierBillState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<PagedResult<TravelSupplierBillListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelSupplierBillListFilter filter, int page, int pageSize,
        CancellationToken cancellationToken = default);
    Task<TravelSupplierBillDto?> GetDetailAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelSupplierBillListItemDto>> GetOpenBillsAsync(int tenantId, int supplierId, CancellationToken cancellationToken = default);
    /// <summary>The supplier's services with a cost that are on no live bill (plus those on <paramref name="includeBillId"/>).</summary>
    Task<IReadOnlyList<TravelBillableCostDto>> GetBillableCostsAsync(int tenantId, int supplierId, string? currencyCode, int? includeBillId,
        CancellationToken cancellationToken = default);
    Task<bool> SupplierReferenceInUseAsync(int tenantId, int supplierId, string reference, int? excludeBillId, CancellationToken cancellationToken = default);
    /// <summary>The context (booking / visa case / group) exists in this tenant.</summary>
    Task<bool> ContextExistsAsync(int tenantId, string contextType, int contextId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(int tenantId, TravelSupplierBillHeaderWrite header, IReadOnlyList<TravelSupplierBillLineWrite> lines, int? userId,
        CancellationToken cancellationToken = default);
    Task<bool> UpdateDraftAsync(int tenantId, int id, TravelSupplierBillHeaderWrite header, IReadOnlyList<TravelSupplierBillLineWrite> lines, int? userId,
        CancellationToken cancellationToken = default);
    Task<string?> PostAsync(int tenantId, int id, DateTime billDate, DateTime dueDate, int? userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<bool> VoidAsync(int tenantId, int id, string reason, int? userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<bool> DeleteDraftAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelPayablesCurrencySummaryDto>> GetCurrencySummaryAsync(int tenantId, TravelBranchScope scope, DateTime today, DateTime monthStart,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelSupplierBalanceDto>> GetSupplierBalancesAsync(int tenantId, TravelBranchScope scope, int? supplierId, DateTime today, bool openOnly,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelStatementMovement>> GetStatementMovementsAsync(int tenantId, int supplierId, string currencyCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetSupplierCurrenciesAsync(int tenantId, int supplierId, CancellationToken cancellationToken = default);
}
