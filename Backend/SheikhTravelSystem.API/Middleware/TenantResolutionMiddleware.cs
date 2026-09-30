using System.Security.Claims;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Multitenancy;

namespace SheikhTravelSystem.API.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public const string TenantIdHeader = "X-Tenant-Id";
    public const string TenantSlugHeader = "X-Tenant-Slug";

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantLookupService tenantLookup,
        IConfiguration configuration)
    {
        int? jwtTenantId = null;
        int? headerTenantId = null;
        string? slug = null;

        var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
        if (int.TryParse(tenantClaim, out var fromJwt))
            jwtTenantId = fromJwt;

        if (context.Request.Headers.TryGetValue(TenantIdHeader, out var tidHeader)
            && int.TryParse(tidHeader.FirstOrDefault(), out var fromHeader))
        {
            headerTenantId = fromHeader;
        }

        if (context.Request.Headers.TryGetValue(TenantSlugHeader, out var slugHeader)
            && !string.IsNullOrWhiteSpace(slugHeader.FirstOrDefault()))
        {
            slug = slugHeader.FirstOrDefault()!.Trim().ToLowerInvariant();
        }

        if (string.IsNullOrEmpty(slug) && context.Request.Query.TryGetValue("tenant", out var tenantQuery))
        {
            slug = tenantQuery.FirstOrDefault()?.Trim().ToLowerInvariant();
        }

        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
        var allowAnonymousDefault = configuration.GetValue("MultiTenancy:AllowAnonymousDefaultTenant", true);

        int? slugResolvedTenantId = null;
        if (!string.IsNullOrEmpty(slug))
        {
            try
            {
                slugResolvedTenantId = await tenantLookup.GetTenantIdBySlugAsync(slug, context.RequestAborted);
            }
            catch (Microsoft.Data.SqlClient.SqlException) when (
                allowAnonymousDefault
                && !isAuthenticated
                && string.Equals(slug, "default", StringComparison.OrdinalIgnoreCase))
            {
                // Unauthenticated slug lookup should not 500 when SQL is down and default is allowed.
                slugResolvedTenantId = TenantResolutionPolicy.AnonymousDefaultTenantId;
            }
        }

        // Preserve existing authenticated fallback when JWT lacks tenant_id.
        if (isAuthenticated && !jwtTenantId.HasValue)
        {
            var userIdClaim = context.User.FindFirst("userId")?.Value
                ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                try
                {
                    jwtTenantId = await tenantLookup.GetTenantIdByUserIdAsync(userId, context.RequestAborted);
                }
                catch (Microsoft.Data.SqlClient.SqlException)
                {
                    // Fall through — handler may return a clearer error.
                }
            }
        }

        var isPlatformOperator = PlatformRoleClaims.IsPlatformOperator(context.User);
        var decision = TenantResolutionPolicy.Decide(
            jwtTenantId,
            headerTenantId,
            slugResolvedTenantId,
            slug,
            isAuthenticated,
            isPlatformOperator,
            allowAnonymousDefault);

        if (decision.Reject)
        {
            logger.LogWarning(
                "Tenant resolution rejected. Path={Path} UserId={UserId} JwtTenantId={JwtTenantId} HeaderTenantId={HeaderTenantId} Reason={Reason}",
                context.Request.Path.Value,
                GetUserId(context.User),
                jwtTenantId,
                headerTenantId,
                decision.RejectReason);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Forbidden" });
            return;
        }

        if (decision.TenantId.HasValue)
            tenantContext.SetTenant(decision.TenantId.Value, decision.Slug);

        if (!string.IsNullOrEmpty(decision.WarnReason))
        {
            logger.LogWarning(
                "Tenant resolution warning. Path={Path} UserId={UserId} JwtTenantId={JwtTenantId} HeaderTenantId={HeaderTenantId} ResolvedTenantId={ResolvedTenantId} Warn={Warn}",
                context.Request.Path.Value,
                GetUserId(context.User),
                jwtTenantId,
                headerTenantId,
                decision.TenantId,
                decision.WarnReason);
        }

        await next(context);
    }

    private static string? GetUserId(ClaimsPrincipal user) =>
        user.FindFirst("userId")?.Value
        ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
