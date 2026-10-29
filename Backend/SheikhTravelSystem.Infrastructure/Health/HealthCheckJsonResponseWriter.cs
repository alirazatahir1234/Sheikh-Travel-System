using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SheikhTravelSystem.Infrastructure.Health;

/// <summary>
/// JSON health payload with per-check status. Omits exception details (connection strings / secrets).
/// </summary>
public static class HealthCheckJsonResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new Dictionary<string, object?>
        {
            ["status"] = report.Status.ToString(),
            ["totalDurationMs"] = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            ["entries"] = report.Entries.ToDictionary(
                static e => e.Key,
                static e => (object)new Dictionary<string, object?>
                {
                    ["status"] = e.Value.Status.ToString(),
                    ["description"] = e.Value.Description,
                    ["durationMs"] = Math.Round(e.Value.Duration.TotalMilliseconds, 2)
                })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
