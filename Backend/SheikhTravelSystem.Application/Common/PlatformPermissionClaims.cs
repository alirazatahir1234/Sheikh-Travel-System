using System.Security.Claims;

namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Permission + platform-tenant claim helpers (SG-05). No role-name authorization.
/// </summary>
public static class PlatformPermissionClaims
{
    public static bool TryGetTokenTenantId(ClaimsPrincipal? user, out int tenantId)
    {
        tenantId = 0;
        var raw = user?.FindFirst("tenant_id")?.Value;
        return int.TryParse(raw, out tenantId);
    }

    public static bool IsPlatformTenantToken(ClaimsPrincipal? user) =>
        TryGetTokenTenantId(user, out var tenantId) && tenantId == PlatformTenant.Id;

    public static bool HasPermissionClaim(ClaimsPrincipal? user, string permission) =>
        user?.HasClaim("permission", permission) == true;

    /// <summary>X-Tenant-Id / act-as: DataRead claim on a platform-tenant token.</summary>
    public static bool CanSwitchTenant(ClaimsPrincipal? user) =>
        IsPlatformTenantToken(user) && HasPermissionClaim(user, PlatformPermissions.TenantsDataRead);

    /// <summary>Subscription middleware exemption: any platform-scope claim on platform tenant.</summary>
    public static bool HasPlatformScopeExemption(ClaimsPrincipal? user)
    {
        if (!IsPlatformTenantToken(user) || user is null)
            return false;

        foreach (var claim in user.FindAll("permission"))
        {
            if (PlatformPermissions.IsPlatformScopePermission(claim.Value))
                return true;
        }

        return false;
    }

    /// <summary>Platform.Tenants.Manage on a platform-tenant token (replaces role-name IsPlatformSuperAdmin).</summary>
    public static bool IsPlatformOperator(ClaimsPrincipal? user) =>
        IsPlatformTenantToken(user) && HasPermissionClaim(user, PlatformPermissions.TenantsManage);
}
