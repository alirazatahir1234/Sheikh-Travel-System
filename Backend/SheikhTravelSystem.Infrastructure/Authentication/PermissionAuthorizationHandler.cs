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
        // Platform-scope permissions: claim + JWT tenant_id == platform tenant (SG-05 A2).
        if (PlatformPermissions.IsPlatformScopePermission(requirement.Permission))
        {
            if (context.User.HasClaim("permission", requirement.Permission)
                && PlatformPermissionClaims.IsPlatformTenantToken(context.User))
            {
                context.Succeed(requirement);
            }

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

        // Gps.DeviceManage previously gated zone writes; keep it accepted for Gps.GeofenceManage.
        if (requirement.Permission == GpsPermissions.GeofenceManage &&
            context.User.HasClaim("permission", GpsPermissions.DeviceManage))
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

        return Task.CompletedTask;
    }

    /// <summary>Obsolete name — use <see cref="PlatformPermissions.IsPlatformScopePermission"/>.</summary>
    public static bool IsSuperAdminOnlyPermission(string permission) =>
        PlatformPermissions.IsPlatformScopePermission(permission);

    /// <summary>Alias for call sites / tests migrating to SG-05 naming.</summary>
    public static bool IsPlatformScopePermission(string permission) =>
        PlatformPermissions.IsPlatformScopePermission(permission);
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
            .Concat(RentalPermissions.All)
            .Concat(TravelPermissions.All))
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
