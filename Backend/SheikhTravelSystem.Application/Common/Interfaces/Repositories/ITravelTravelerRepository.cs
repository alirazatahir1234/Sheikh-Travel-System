using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Travelers of travel customers. Every method is scoped by the explicit <c>tenantId</c>; branch scope
/// is checked by handlers through the owning customer. Returned identity values are raw.
/// </summary>
public interface ITravelTravelerRepository
{
    Task<IReadOnlyList<TravelTravelerDto>> GetByCustomerAsync(int customerId, int tenantId, CancellationToken cancellationToken = default);
    Task<TravelTravelerDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    /// <summary>Travelers of the tenant with the given ids (unknown / other-tenant ids are simply absent).</summary>
    Task<IReadOnlyList<TravelTravelerDto>> GetByIdsAsync(IReadOnlyList<int> ids, int tenantId, CancellationToken cancellationToken = default);
    Task<TravelTravelerIdentity?> GetIdentityAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<bool> PassportExistsAsync(int tenantId, string issuingCountryCode, string passportNumber, int? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Returns 0 when the customer is not an active travel customer of the tenant.</summary>
    Task<int> CreateAsync(int tenantId, int customerId, SaveTravelTravelerDto dto, int? userId, CancellationToken cancellationToken = default);

    /// <summary><paramref name="dto"/> carries the final passport/CNIC values to store.</summary>
    Task<bool> UpdateAsync(int id, int tenantId, SaveTravelTravelerDto dto, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the traveler and clears it as mahram of others, in one transaction.</summary>
    Task<bool> DeleteAsync(int id, int tenantId, CancellationToken cancellationToken = default);
}
