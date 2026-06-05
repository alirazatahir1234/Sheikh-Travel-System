namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ITenantContext
{
    int? TenantId { get; }
    string? TenantSlug { get; }
    void SetTenant(int tenantId, string? slug = null);
    int GetRequiredTenantId();
}
