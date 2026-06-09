using Microsoft.AspNetCore.Authorization;

namespace SheikhTravelSystem.API.Authorization;

/// <summary>
/// Requires a platform permission claim (or legacy Admin / SUPER_ADMIN bypass).
/// </summary>
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission) => Policy = permission;
}
