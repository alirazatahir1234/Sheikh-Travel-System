namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Resolves the display timezone for a tenant or one of its vehicles:
/// vehicle's branch → tenant (TenantBranding.TimeZone) → platform default (Localization:DefaultTimeZone).
/// Every lookup is scoped to the given tenant.
/// </summary>
public interface ITenantTimeZoneResolver
{
    /// <summary>Platform default IANA id (configured, e.g. Asia/Karachi).</summary>
    string DefaultTimeZoneId { get; }

    /// <summary>Tenant's timezone id, or the platform default when none (or an invalid one) is set.</summary>
    Task<string> GetTenantTimeZoneIdAsync(int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Branch timezone of the vehicle, else the tenant's, else the default. Other tenants' vehicles resolve to the caller tenant's zone.</summary>
    Task<string> GetVehicleTimeZoneIdAsync(int vehicleId, int tenantId, CancellationToken cancellationToken = default);

    Task<TimeZoneInfo> GetTenantTimeZoneAsync(int tenantId, CancellationToken cancellationToken = default);
}
