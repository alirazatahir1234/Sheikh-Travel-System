using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Authentication;

public class TenantContext : ITenantContext
{
    private int? _tenantId;
    private string? _tenantSlug;

    public int? TenantId => _tenantId;
    public string? TenantSlug => _tenantSlug;

    public void SetTenant(int tenantId, string? slug = null)
    {
        _tenantId = tenantId;
        _tenantSlug = slug;
    }

    public int GetRequiredTenantId() =>
        _tenantId ?? throw new InvalidOperationException("Tenant context is not set for this request.");
}
