using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>Exchange rates, supplier rate cards and supplier commission rules. Every statement filters on TenantId.</summary>
public interface ITravelSupplierSetupRepository
{
    Task<IReadOnlyList<TravelExchangeRateDto>> GetExchangeRatesAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<int> SaveExchangeRateAsync(int tenantId, int? id, SaveTravelExchangeRateDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteExchangeRateAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelSupplierRateDto>> GetRatesAsync(int tenantId, int supplierId, CancellationToken cancellationToken = default);
    Task<int> SaveRateAsync(int tenantId, int supplierId, int? id, SaveTravelSupplierRateDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRateAsync(int tenantId, int supplierId, int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelSupplierCommissionRuleDto>> GetCommissionRulesAsync(int tenantId, int supplierId, CancellationToken cancellationToken = default);
    Task<int> SaveCommissionRuleAsync(int tenantId, int supplierId, int? id, SaveTravelSupplierCommissionRuleDto dto, int? userId,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteCommissionRuleAsync(int tenantId, int supplierId, int id, CancellationToken cancellationToken = default);
}
