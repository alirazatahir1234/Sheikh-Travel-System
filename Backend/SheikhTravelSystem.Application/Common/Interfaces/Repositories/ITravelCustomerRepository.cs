using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Travel customers (Customers + TravelCustomerProfiles). All methods are scoped by the explicit
/// <c>tenantId</c>; list/get also apply the caller's <see cref="TravelBranchScope"/>.
/// Returned CNIC values are raw — masking is applied by the handler.
/// </summary>
public interface ITravelCustomerRepository
{
    Task<PagedResult<TravelCustomerListItemDto>> GetPagedAsync(
        int tenantId, TravelBranchScope scope, int page, int pageSize,
        string? search, string? customerKind, int? agentUserId, int? branchId,
        CancellationToken cancellationToken = default);

    Task<TravelCustomerDto?> GetByIdAsync(int customerId, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);

    /// <summary>Active travel customer reference, or null when not a travel customer of the tenant.</summary>
    Task<TravelCustomerRef?> GetRefAsync(int customerId, int tenantId, CancellationToken cancellationToken = default);

    Task<ExistingCustomerState> GetExistingCustomerStateAsync(int customerId, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Id of another non-deleted customer in the tenant with this CNIC, if any.</summary>
    Task<int?> FindCustomerIdByCnicAsync(int tenantId, string cnic, int? excludeCustomerId, CancellationToken cancellationToken = default);

    /// <summary>Null when every reference belongs to the tenant; otherwise a user-facing error.</summary>
    Task<string?> ValidateReferencesAsync(
        int tenantId, int? agentUserId, int? branchId, int? leadSourceId, CancellationToken cancellationToken = default);

    /// <summary>Creates (or attaches to an existing) Customers row and the travel profile in one transaction.</summary>
    Task<int> CreateAsync(int tenantId, CreateTravelCustomerDto dto, int? userId, CancellationToken cancellationToken = default);

    /// <summary><paramref name="cnic"/> is the final value to store (already resolved for "keep").</summary>
    Task<bool> UpdateAsync(int customerId, int tenantId, UpdateTravelCustomerDto dto, string? cnic, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the travel profile only; the shared Customers row is left untouched.</summary>
    Task<bool> DeleteAsync(int customerId, int tenantId, CancellationToken cancellationToken = default);

    Task<string?> GetCnicAsync(int customerId, int tenantId, CancellationToken cancellationToken = default);
}
