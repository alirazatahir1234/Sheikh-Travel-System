namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Resolves tenant identity from slug or user without exposing SQL to the API layer.
/// </summary>
public interface ITenantLookupService
{
    Task<int?> GetTenantIdBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<int?> GetTenantIdByUserIdAsync(int userId, CancellationToken cancellationToken = default);
}
