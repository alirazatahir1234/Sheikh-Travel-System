using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Authentication;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public int? UserId
    {
        get
        {
            var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst("userId")?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : null;
        }
    }

    public string? Role => Roles.FirstOrDefault();

    public IReadOnlyList<string> Roles
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user is null) return Array.Empty<string>();
            return user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        }
    }

    public int? DriverId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User.FindFirst("driver_id")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool HasPermission(string permission)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null) return false;

        return user.HasClaim("permission", permission)
            || user.HasClaim("role", PlatformRoles.SuperAdmin)
            || user.IsInRole(PlatformRoles.SuperAdmin);
    }

    public bool IsPlatformSuperAdmin
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user is null) return false;
            return user.HasClaim("role", PlatformRoles.SuperAdmin)
                   || user.IsInRole(PlatformRoles.SuperAdmin);
        }
    }
}
