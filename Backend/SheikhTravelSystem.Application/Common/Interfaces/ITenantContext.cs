namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ITenantContext
{
    int? TenantId { get; }
    string? TenantSlug { get; }

    /// <summary>
    /// True when a platform SUPER_ADMIN request is not locked to one company
    /// (no X-Tenant-Id). Admin cross-tenant read APIs may query all tenants.
    /// Legacy tenant-scoped handlers should still prefer <see cref="TenantId"/> when set.
    /// </summary>
    bool IsCrossTenantBypass { get; }

    void SetTenant(int tenantId, string? slug = null);

    /// <summary>Marks the request as platform cross-tenant (optional working tenant may still be set).</summary>
    void SetCrossTenantBypass(bool enabled);

    int GetRequiredTenantId();
}
