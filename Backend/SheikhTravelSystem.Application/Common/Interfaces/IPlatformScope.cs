namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IPlatformScope
{
    int TenantId { get; }
    bool IsSuperAdmin { get; }
    bool HasPermission(string permission);
    void EnsureTenantAccess(int tenantId);
}
