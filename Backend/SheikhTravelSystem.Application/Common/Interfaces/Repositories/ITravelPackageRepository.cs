using SheikhTravelSystem.Application.Features.TravelNexus.Packages.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>Umrah/Hajj packages with default stays and prices. Every statement is scoped by <c>tenantId</c>.</summary>
public interface ITravelPackageRepository
{
    Task<IReadOnlyList<TravelPackageListItemDto>> GetListAsync(int tenantId, string? status, string? packageType, CancellationToken cancellationToken = default);
    /// <summary>Package with stays and prices.</summary>
    Task<TravelPackageDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default);
    /// <summary>Inserts package, stays and prices in one transaction (status Draft).</summary>
    Task<int> CreateAsync(int tenantId, SaveTravelPackageDto dto, int? userId, CancellationToken cancellationToken = default);
    /// <summary>Updates a non-archived package and replaces its stays and prices in one transaction.</summary>
    Task<bool> UpdateAsync(int id, int tenantId, SaveTravelPackageDto dto, CancellationToken cancellationToken = default);
    Task<bool> SetStatusAsync(int id, int tenantId, string expectedStatus, string newStatus, CancellationToken cancellationToken = default);
    Task<bool> DeleteDraftAsync(int id, int tenantId, CancellationToken cancellationToken = default);
}
