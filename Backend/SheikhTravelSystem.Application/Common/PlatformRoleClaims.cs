using System.Security.Claims;

namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Shared platform role claim checks (IsInRole + "role" + ClaimTypes.Role).
/// Used by authorization and tenant resolution — do not duplicate.
/// </summary>
public static class PlatformRoleClaims
{
    public static bool HasRole(ClaimsPrincipal? user, string roleCode) =>
        user?.IsInRole(roleCode) == true
        || user?.HasClaim("role", roleCode) == true
        || user?.HasClaim(ClaimTypes.Role, roleCode) == true;

    /// <summary>Platform operator = SUPER_ADMIN only (not TENANT_ADMIN).</summary>
    public static bool IsPlatformOperator(ClaimsPrincipal? user) =>
        HasRole(user, PlatformRoles.SuperAdmin);
}
