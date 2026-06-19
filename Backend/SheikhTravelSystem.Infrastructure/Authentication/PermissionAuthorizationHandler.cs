using Microsoft.AspNetCore.Authorization;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Infrastructure.Authentication;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.Permission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (HasRole(context, PlatformRoles.SuperAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.IsInRole("Admin") &&
            requirement.Permission.StartsWith("Platform.", StringComparison.Ordinal))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static bool HasRole(AuthorizationHandlerContext context, string roleCode) =>
        context.User.HasClaim("role", roleCode) || context.User.IsInRole(roleCode);
}

public static class PermissionPolicyRegistration
{
    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        foreach (var permission in PlatformPermissions.All.Concat(FleetPermissions.All).Concat(DriverPermissions.All))
        {
            options.AddPolicy(permission, policy =>
                policy.Requirements.Add(new PermissionRequirement(permission)));
        }
    }
}
