using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Users;

/// <summary>Non-SQL helpers for UserRoles assignment policy.</summary>
internal static class UserRoleAssignment
{
    /// <summary>
    /// Blocks elevating a user to SUPER_ADMIN unless the caller is a platform Super Admin.
    /// </summary>
    public static void EnsureCanAssignPlatformRole(string? platformRoleCode, ICurrentUserService currentUser)
    {
        if (string.IsNullOrWhiteSpace(platformRoleCode))
            return;

        if (string.Equals(platformRoleCode.Trim(), PlatformRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            && !currentUser.IsPlatformSuperAdmin)
        {
            throw new ForbiddenException("Only platform owners can assign the Super Admin role.");
        }
    }
}
