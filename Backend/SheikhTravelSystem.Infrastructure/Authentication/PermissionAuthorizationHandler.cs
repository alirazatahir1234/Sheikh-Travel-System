using System.Security.Claims;
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
        if (PlatformRoleClaims.HasRole(context.User, PlatformRoles.SuperAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // SuperAdmin-only permissions (Gps.Command*, Platform.Tenants.*, Platform.Gps.*,
        // Platform.Migrations.*, Platform.System.*) must never succeed for tenant roles,
        // even if a stale token or legacy RolePermissions row carries the claim.
        if (IsSuperAdminOnlyPermission(requirement.Permission))
        {
            return Task.CompletedTask;
        }

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

        // Legacy Ai.View / Ai.Manage satisfy finer AI Operations codes (backward compatible).
        if (AiPermissions.ImpliedByView.Contains(requirement.Permission) &&
            context.User.HasClaim("permission", AiPermissions.View))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (AiPermissions.ImpliedByManage.Contains(requirement.Permission) &&
            context.User.HasClaim("permission", AiPermissions.Manage))
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
        if (PlatformRoleClaims.HasRole(context.User, PlatformRoles.TenantAdmin) &&
            requirement.Permission.StartsWith("Platform.", StringComparison.Ordinal) &&
            !requirement.Permission.StartsWith("Platform.Tenants.", StringComparison.Ordinal))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Attendance / driver roster: management roles match mobile SettingsAccess gates when
        // JWT permission claims lag RolePermissions seed (do not strip RequirePermission).
        if (IsDriverOpsRoleBypass(context.User, requirement.Permission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    public static bool IsSuperAdminOnlyPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission)) return false;
        return permission.StartsWith("Gps.Command", StringComparison.OrdinalIgnoreCase)
            || permission.StartsWith("Platform.Tenants.", StringComparison.OrdinalIgnoreCase)
            || permission.StartsWith("Platform.Gps.", StringComparison.OrdinalIgnoreCase)
            || permission.StartsWith("Platform.Migrations.", StringComparison.OrdinalIgnoreCase)
            || permission.StartsWith("Platform.System.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// TENANT_ADMIN / OWNER / FLEET_MANAGER / DRIVER_MANAGER may view or correct attendance
    /// when they hold the matching management role even if Driver.View / Driver.Update claims
    /// were omitted from an older token.
    /// </summary>
    private static bool IsDriverOpsRoleBypass(ClaimsPrincipal user, string permission)
    {
        if (permission is not (DriverPermissions.DriverView or DriverPermissions.DriverUpdate))
            return false;

        return PlatformRoleClaims.HasRole(user, PlatformRoles.TenantAdmin)
            || PlatformRoleClaims.HasRole(user, "OWNER")
            || PlatformRoleClaims.HasRole(user, "FLEET_MANAGER")
            || PlatformRoleClaims.HasRole(user, "DRIVER_MANAGER");
    }
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
            .Concat(NotificationPermissions.All)
            .Concat(WebsitePermissions.All)
            .Concat(WhatsAppPermissions.All)
            .Concat(SmartOpsPermissions.All)
            .Concat(RentalPermissions.All))
        {
            options.AddPolicy(permission, policy =>
                policy.Requirements.Add(new PermissionRequirement(permission)));
        }

        // Personal driver-app attendance: require authenticated driver identity.
        // Uses PlatformRoleClaims (role / ClaimTypes.Role / primary_role, case-insensitive)
        // instead of Authorize(Roles=…) which only checks RoleClaimType via IsInRole.
        options.AddPolicy(AttendanceAuthPolicies.DriverSelf, policy =>
            policy.RequireAuthenticatedUser()
                .RequireAssertion(ctx =>
                {
                    var user = ctx.User;
                    if (!PlatformRoleClaims.IsDriver(user))
                        return false;
                    var driverId = user.FindFirst("driver_id")?.Value;
                    return !string.IsNullOrWhiteSpace(driverId);
                }));
    }
}
