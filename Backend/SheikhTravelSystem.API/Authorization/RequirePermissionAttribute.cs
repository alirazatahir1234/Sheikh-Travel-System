using Microsoft.AspNetCore.Authorization;

namespace SheikhTravelSystem.API.Authorization;

/// <summary>
/// Requires a permission claim (platform-scope also requires JWT tenant_id = platform tenant).
/// </summary>
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission) => Policy = permission;
}
