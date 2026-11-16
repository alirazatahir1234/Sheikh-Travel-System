namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Resolves tenant identity from slug or user without exposing SQL to the API layer.
/// </summary>
public interface ITenantLookupService
{
    Task<int?> GetTenantIdBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<int?> GetTenantIdByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the tenant exists and is not soft-deleted (includes suspended/expired/deactivated).
    /// Used to validate X-Tenant-Id against the switchable set (SG-11).
    /// </summary>
    Task<bool> IsSwitchableTenantAsync(int tenantId, CancellationToken cancellationToken = default);
}
