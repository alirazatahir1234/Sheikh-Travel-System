namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IPlatformScope
{
    int TenantId { get; }
    bool HasPermission(string permission);
    void EnsureTenantAccess(int tenantId);

    /// <summary>
    /// Resolves the effective tenant for a request. When <paramref name="requestedTenantId"/>
    /// is set (e.g. platform operator company deep-link), access is authorized then that id is used;
    /// otherwise the JWT / middleware tenant is used.
    /// </summary>
    int ResolveTenantId(int? requestedTenantId);
}
