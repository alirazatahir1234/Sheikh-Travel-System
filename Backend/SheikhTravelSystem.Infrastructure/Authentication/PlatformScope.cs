using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
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
        // SUPER_ADMIN may scope lists to another tenant via ?tenantId= (ERP company deep-link).
        if (IsSuperAdmin) return;
        if (tenantId != TenantId)
            throw new ForbiddenException("Cross-tenant access is not allowed.");
    }

    private bool HasRole(string roleCode) =>
        User?.IsInRole(roleCode) == true ||
        User?.HasClaim("role", roleCode) == true ||
        User?.HasClaim(ClaimTypes.Role, roleCode) == true;
}
