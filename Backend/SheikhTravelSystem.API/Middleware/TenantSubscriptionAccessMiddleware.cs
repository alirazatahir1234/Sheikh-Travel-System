using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.API.Middleware;

/// <summary>
/// Hard-gates authenticated ERP/driver API calls when the resolved tenant is deactivated
/// or the subscription is Expired. Platform Super Admin is exempt so billing can be fixed.
/// </summary>
public sealed class TenantSubscriptionAccessMiddleware(RequestDelegate next)
{
    public const string CodeTenantDeactivated = "tenant_deactivated";
    public const string CodeSubscriptionExpired = "subscription_expired";

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IPlatformRepository platformRepository)
    {
        var path = context.Request.Path.Value ?? "";
        if (context.User.Identity?.IsAuthenticated != true
            || IsExemptPath(path)
            || PlatformPermissionClaims.HasPlatformScopeExemption(context.User))
        {
            await next(context);
            return;
        }

        var tenantId = tenantContext.TenantId ?? 0;
        if (tenantId <= 0)
        {
            await next(context);
            return;
        }

        var access = await platformRepository.GetTenantSubscriptionAccessAsync(tenantId, context.RequestAborted);
        if (access is null)
        {
            await next(context);
            return;
        }

        var (tenantActive, status, _, _) = access.Value;
        if (!tenantActive)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "This company account is deactivated. Contact SheikhGo billing to restore access.",
                code = CodeTenantDeactivated
            });
            return;
        }

        if (string.Equals(status, SubscriptionBillingLifecycle.StatusExpired, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Your subscription has expired. Contact SheikhGo billing to renew.",
                code = CodeSubscriptionExpired
            });
            return;
        }

        await next(context);
    }

    private static bool IsExemptPath(string path)
    {
        // Auth + health + public website must remain reachable.
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.StartsWith("/api/website", StringComparison.OrdinalIgnoreCase))
            return true;
        // Platform admin APIs remain available to platform-scope operators (checked above).
        // Non-admin users hitting /api/platform still go through the gate.
        if (path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
