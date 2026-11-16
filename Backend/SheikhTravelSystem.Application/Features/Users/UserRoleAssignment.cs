using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Users;

/// <summary>Non-SQL helpers for UserRoles assignment policy.</summary>
internal static class UserRoleAssignment
{
    /// <summary>
    /// Platform roles (SUPER_ADMIN / PLATFORM_AUDITOR) require Platform.Tenants.Manage
    /// and may only be assigned to users in the platform tenant (SG-05 B11).
    /// </summary>
    public static void EnsureCanAssignPlatformRole(
        string? platformRoleCode,
        ICurrentUserService currentUser,
        int targetUserTenantId)
    {
        if (string.IsNullOrWhiteSpace(platformRoleCode))
            return;

        var code = platformRoleCode.Trim();
        var isPlatformRole =
            string.Equals(code, PlatformRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, PlatformRoles.PlatformAuditor, StringComparison.OrdinalIgnoreCase);

        if (!isPlatformRole)
            return;

        if (!currentUser.HasPermission(PlatformPermissions.TenantsManage)
            && !currentUser.IsPlatformSuperAdmin)
        {
            throw new ForbiddenException("Only platform operators can assign platform roles.");
        }

        if (targetUserTenantId != PlatformTenant.Id)
        {
            throw new ForbiddenException(
                "Platform roles can only be assigned to users in the platform tenant.");
        }
    }
}
