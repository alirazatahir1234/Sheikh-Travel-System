using System.Security.Claims;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.API.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string TenantIdHeader = "X-Tenant-Id";
    public const string TenantSlugHeader = "X-Tenant-Slug";

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, IDbConnectionFactory dbFactory)
    {
        int? tenantId = null;
        string? slug = null;

        var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
        if (int.TryParse(tenantClaim, out var fromJwt))
        {
            tenantId = fromJwt;
        }

        if (context.Request.Headers.TryGetValue(TenantIdHeader, out var tidHeader)
            && int.TryParse(tidHeader.FirstOrDefault(), out var fromHeader))
        {
            tenantId = fromHeader;
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

        if (!tenantId.HasValue && !string.IsNullOrEmpty(slug))
        {
            using var connection = dbFactory.CreateConnection();
            tenantId = await connection.ExecuteScalarAsync<int?>(
                "SELECT Id FROM Tenants WHERE Slug = @Slug AND IsActive = 1",
                new { Slug = slug });
        }

        if (!tenantId.HasValue && context.User.Identity?.IsAuthenticated != true)
        {
            tenantId = 1;
            slug ??= "default";
        }

        if (tenantId.HasValue)
        {
            tenantContext.SetTenant(tenantId.Value, slug);
        }

        await next(context);
    }
}
