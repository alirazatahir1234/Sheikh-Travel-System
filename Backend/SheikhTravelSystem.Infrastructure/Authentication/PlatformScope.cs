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

    public bool HasPermission(string permission)
    {
        if (User is null || string.IsNullOrWhiteSpace(permission))
            return false;

        if (!User.HasClaim("permission", permission))
            return false;

        // Platform-scope: claim alone is not enough — JWT must be platform tenant (A2/A8).
        if (PlatformPermissions.IsPlatformScopePermission(permission))
            return PlatformPermissionClaims.IsPlatformTenantToken(User);

        return true;
    }

    public void EnsureTenantAccess(int tenantId)
    {
        if (tenantId == TenantId)
            return;

        if (!HasPermission(PlatformPermissions.TenantsDataRead))
            throw new ForbiddenException("Cross-tenant access is not allowed.");
    }

    public int ResolveTenantId(int? requestedTenantId)
    {
        if (requestedTenantId.HasValue)
        {
            EnsureTenantAccess(requestedTenantId.Value);
            return requestedTenantId.Value;
        }

        return TenantId;
    }
}
