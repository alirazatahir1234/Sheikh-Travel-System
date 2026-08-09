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

        // Stage 14: legacy AuditLogs.View satisfies Audit.View
        if (requirement.Permission == PlatformPermissions.AuditView &&
            context.User.HasClaim("permission", PlatformPermissions.AuditLogsView))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (HasRole(context, PlatformRoles.SuperAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Legacy Admin retains Platform.* bypass for platform-tenant operators still on Users.Role=Admin.
        // New tenants should use SUPER_ADMIN / TENANT_ADMIN with explicit RolePermissions.
        if (context.User.IsInRole("Admin") &&
            requirement.Permission.StartsWith("Platform.", StringComparison.Ordinal))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // TENANT_ADMIN role claim also satisfies Platform tenant-admin surface when permissions lag seed.
        if (HasRole(context, PlatformRoles.TenantAdmin) &&
            requirement.Permission.StartsWith("Platform.", StringComparison.Ordinal) &&
            !requirement.Permission.StartsWith("Platform.Tenants.", StringComparison.Ordinal))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static bool HasRole(AuthorizationHandlerContext context, string roleCode) =>
        context.User.IsInRole(roleCode)
        || context.User.HasClaim("role", roleCode)
        || context.User.HasClaim(System.Security.Claims.ClaimTypes.Role, roleCode);
}

public static class PermissionPolicyRegistration
{
    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        foreach (var permission in PlatformPermissions.All
            .Concat(FleetPermissions.All)
            .Concat(DriverPermissions.All)
            .Concat(MaintenancePermissions.All)
            .Concat(GpsPermissions.All)
            .Concat(OperationsPermissions.All)
            .Concat(FinancePermissions.All)
            .Concat(AnalyticsPermissions.All)
            .Concat(AiPermissions.All)
            .Concat(NotificationPermissions.All))
        {
            options.AddPolicy(permission, policy =>
                policy.Requirements.Add(new PermissionRequirement(permission)));
        }
    }
}
