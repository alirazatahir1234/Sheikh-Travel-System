using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.API.Authorization;

/// <summary>
/// Requires a valid GPS device API key via header <c>X-Gps-Device-Key</c>.
/// Validates against the tenant that owns the device <c>uniqueId</c> (query or body).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class GpsDeviceApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-Gps-Device-Key";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var logger = context.HttpContext.RequestServices.GetService(typeof(ILogger<GpsDeviceApiKeyAttribute>))
            as ILogger<GpsDeviceApiKeyAttribute>;
        var dbFactory = context.HttpContext.RequestServices.GetService(typeof(IDbConnectionFactory))
            as IDbConnectionFactory;
        var configuration = context.HttpContext.RequestServices.GetService(typeof(IConfiguration))
            as IConfiguration;

        if (dbFactory is null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
        {
            logger?.LogWarning("GPS device callback rejected: missing {Header}", HeaderName);
            context.Result = new UnauthorizedObjectResult(new { message = "GPS device API key required." });
            return;
        }

        var providedKey = keyValues.First()!.Trim();
        var uniqueId = await ResolveUniqueIdAsync(context);
        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            logger?.LogWarning("GPS device callback rejected: uniqueId not provided");
            context.Result = new BadRequestObjectResult(new { message = "uniqueId is required." });
            return;
        }

        using var connection = dbFactory.CreateConnection();
        var tenantId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 TenantId FROM GpsDevices
            WHERE UniqueId = @UniqueId AND IsDeleted = 0
            """,
            new { UniqueId = uniqueId.Trim() },
            cancellationToken: context.HttpContext.RequestAborted));

        if (tenantId is null or <= 0)
        {
            logger?.LogWarning("GPS device callback rejected: unknown uniqueId {UniqueId}", uniqueId);
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid device or API key." });
            return;
        }

        var expectedKeys = new List<string>();

        var tenantGpsKey = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT ApiKey FROM TenantGpsSettings WHERE TenantId = @TenantId",
            new { TenantId = tenantId.Value },
            cancellationToken: context.HttpContext.RequestAborted));
        if (!string.IsNullOrWhiteSpace(tenantGpsKey))
            expectedKeys.Add(tenantGpsKey.Trim());

        var platformKey = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT TOP 1 Value FROM PlatformSettings
            WHERE TenantId = @TenantId AND Category = N'Integrations' AND [Key] = N'GpsDeviceApiKey'
            """,
            new { TenantId = tenantId.Value },
            cancellationToken: context.HttpContext.RequestAborted));
        if (!string.IsNullOrWhiteSpace(platformKey))
            expectedKeys.Add(platformKey.Trim());

        var configKey = configuration?["GpsSettings:DeviceApiKey"];
        if (!string.IsNullOrWhiteSpace(configKey))
            expectedKeys.Add(configKey.Trim());

        if (expectedKeys.Count == 0)
        {
            logger?.LogWarning(
                "GPS device callback rejected: no DeviceApiKey configured for tenant {TenantId}",
                tenantId.Value);
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid device or API key." });
            return;
        }

        var matched = expectedKeys.Any(expected => FixedTimeEquals(providedKey, expected));
        if (!matched)
        {
            logger?.LogWarning(
                "GPS device callback rejected: bad API key for uniqueId {UniqueId} tenant {TenantId}",
                uniqueId, tenantId.Value);
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid device or API key." });
            return;
        }

        context.HttpContext.Items["GpsDeviceTenantId"] = tenantId.Value;
        context.HttpContext.Items["GpsDeviceUniqueId"] = uniqueId.Trim();
    }

    private static async Task<string?> ResolveUniqueIdAsync(AuthorizationFilterContext context)
    {
        var queryId = context.HttpContext.Request.Query["uniqueId"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(queryId))
            return queryId;

        if (context.HttpContext.Request.HasJsonContentType()
            && context.HttpContext.Request.ContentLength is > 0)
        {
            context.HttpContext.Request.EnableBuffering();
            using var reader = new StreamReader(
                context.HttpContext.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.HttpContext.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("uniqueId", out var prop)
                        || doc.RootElement.TryGetProperty("UniqueId", out prop))
                    {
                        return prop.GetString();
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // Body will be re-bound by model binder; treat as missing uniqueId here.
                }
            }
        }

        return null;
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        if (aBytes.Length != bBytes.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
