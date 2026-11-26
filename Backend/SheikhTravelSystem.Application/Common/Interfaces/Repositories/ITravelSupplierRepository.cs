using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>Travel suppliers (airlines, hotels, bed banks, consolidators, transport). Every statement filters on TenantId.</summary>
public interface ITravelSupplierRepository
{
    Task<IReadOnlyList<TravelSupplierDto>> GetListAsync(int tenantId, string? supplierType, bool? isActive, CancellationToken cancellationToken = default);
    Task<TravelSupplierDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(int tenantId, string name, int? excludeId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(int tenantId, SaveTravelSupplierDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, int tenantId, SaveTravelSupplierDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, int tenantId, CancellationToken cancellationToken = default);
}
