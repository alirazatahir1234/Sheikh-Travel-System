namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Looks up GPS device ownership and configured device API keys for callback auth.
/// </summary>
public interface IGpsDeviceAuthService
{
    Task<int?> GetTenantIdByUniqueIdAsync(string uniqueId, CancellationToken cancellationToken = default);

    Task<string?> GetTenantGpsApiKeyAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<string?> GetPlatformGpsDeviceApiKeyAsync(int tenantId, CancellationToken cancellationToken = default);
}
