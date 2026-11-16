namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ITenantContext
{
    int? TenantId { get; }
    string? TenantSlug { get; }

    /// <summary>
    /// True only when an endpoint opted in via <c>AllowCrossTenantBypass</c> and the
    /// caller has platform DataRead with no X-Tenant-Id (SG-11). Default is never set.
    /// Legacy tenant-scoped handlers should still prefer <see cref="TenantId"/> when set.
    /// </summary>
    bool IsCrossTenantBypass { get; }

    void SetTenant(int tenantId, string? slug = null);

    /// <summary>Marks the request as platform cross-tenant (optional working tenant may still be set).</summary>
    void SetCrossTenantBypass(bool enabled);

    int GetRequiredTenantId();
}
