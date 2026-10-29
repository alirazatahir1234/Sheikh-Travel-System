using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SheikhTravelSystem.Infrastructure.Health;

/// <summary>
/// Plain-text health body for uptime probes (ADO deploy script, Caddy, curl).
/// Body is exactly <c>Healthy</c>, <c>Degraded</c>, or <c>Unhealthy</c>.
/// </summary>
public static class HealthCheckPlainTextResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(report.Status.ToString());
    }
}
