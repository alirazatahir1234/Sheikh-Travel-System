using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Authentication;

public class PlatformScope(IHttpContextAccessor httpContextAccessor, ITenantContext tenantContext)
    : IPlatformScope
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public int TenantId => tenantContext.GetRequiredTenantId();

    public bool IsSuperAdmin =>
        HasRole(PlatformRoles.SuperAdmin);

    public bool HasPermission(string permission)
    {
        if (IsSuperAdmin) return true;
        if (User?.HasClaim("permission", permission) == true) return true;

        // Legacy Admin role retains platform admin access during migration.
        if (User?.IsInRole("Admin") == true && permission.StartsWith("Platform.", StringComparison.Ordinal))
            return true;

        return false;
    }

    public void EnsureTenantAccess(int tenantId)
    {
        if (IsSuperAdmin) return;
        if (tenantId != TenantId)
            throw new UnauthorizedAccessException("Cross-tenant access is not allowed.");
    }

    private bool HasRole(string roleCode) =>
        User?.HasClaim("role", roleCode) == true ||
        User?.IsInRole(roleCode) == true;
}
