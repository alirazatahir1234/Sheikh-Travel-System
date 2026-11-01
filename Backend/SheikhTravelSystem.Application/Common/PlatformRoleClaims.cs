using System.Security.Claims;

namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Shared platform role claim checks (IsInRole + "role" + ClaimTypes.Role + primary_role).
/// Used by authorization and tenant resolution — do not duplicate.
/// Matching is ordinal-ignore-case so legacy "Driver" and RBAC "DRIVER" both work.
/// </summary>
public static class PlatformRoleClaims
{
    public static bool HasRole(ClaimsPrincipal? user, string roleCode)
    {
        if (user is null || string.IsNullOrWhiteSpace(roleCode))
            return false;

        if (user.IsInRole(roleCode))
            return true;

        // JwtBearer IsInRole is case-sensitive; try the other common casing.
        if (!string.Equals(roleCode, roleCode.ToUpperInvariant(), StringComparison.Ordinal)
            && user.IsInRole(roleCode.ToUpperInvariant()))
            return true;
        if (!string.Equals(roleCode, "Driver", StringComparison.OrdinalIgnoreCase)
            && string.Equals(roleCode, "DRIVER", StringComparison.OrdinalIgnoreCase)
            && user.IsInRole("Driver"))
            return true;

        foreach (var claim in user.Claims)
        {
            if (claim.Type is not ("role" or "primary_role")
                && claim.Type != ClaimTypes.Role)
            {
                continue;
            }

            if (string.Equals(claim.Value, roleCode, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>True when the caller is a driver (legacy enum or RBAC code).</summary>
    public static bool IsDriver(ClaimsPrincipal? user) =>
        HasRole(user, "DRIVER") || HasRole(user, "Driver");

    /// <summary>Platform operator = SUPER_ADMIN only (not TENANT_ADMIN).</summary>
    public static bool IsPlatformOperator(ClaimsPrincipal? user) =>
        HasRole(user, PlatformRoles.SuperAdmin);
}
