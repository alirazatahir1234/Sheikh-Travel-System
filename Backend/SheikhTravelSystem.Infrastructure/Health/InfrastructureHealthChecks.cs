using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Infrastructure.Caching;
using SheikhTravelSystem.Infrastructure.SignalR;
using SheikhTravelSystem.Infrastructure.Traccar;

namespace SheikhTravelSystem.Infrastructure.Health;

public sealed class SqlHealthCheck(IDbConnectionFactory dbFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = dbFactory.CreateConnection();
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition("SELECT 1", cancellationToken: cancellationToken));
            return HealthCheckResult.Healthy("SQL Server reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server unreachable.", ex);
        }
    }
}

public sealed class RedisHealthCheck(
    IOptions<CacheOptions> options,
    IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.RedisConnectionString))
            return HealthCheckResult.Healthy("Redis not configured — using in-memory cache.");

        try
        {
            var cache = services.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            var key = $"health:{Guid.NewGuid():N}";
            await cache.SetStringAsync(key, "ok", cancellationToken);
            var value = await cache.GetStringAsync(key, cancellationToken);
            cache.Remove(key);
            return value == "ok"
                ? HealthCheckResult.Healthy("Redis reachable.")
                : HealthCheckResult.Degraded("Redis read/write mismatch.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis unreachable.", ex);
        }
    }
}

public sealed class TraccarHealthCheck(
    ITraccarClient traccar,
    ITraccarSyncState syncState,
    IOptions<TraccarOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return HealthCheckResult.Healthy("Traccar integration disabled.");

        // Cap wait so /health never blocks probes for tens of seconds when Traccar hangs.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var devices = await traccar.GetDevicesAsync(timeoutCts.Token);
            var snapshot = syncState.Snapshot(connected: true);
            var data = new Dictionary<string, object>
            {
                ["deviceCount"] = devices.Count,
                ["lastPositionSyncAt"] = snapshot.LastPositionSyncAt?.ToString("O") ?? "never",
                ["isRunning"] = snapshot.IsRunning
            };
            return HealthCheckResult.Healthy("Traccar reachable.", data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded("Traccar health check timed out.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Traccar unreachable.", ex);
        }
    }
}

public sealed class SignalRHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["hub"] = "/hubs/tracking",
            ["connectedClients"] = TrackingHubMetrics.ConnectedClients
        };
        return Task.FromResult(HealthCheckResult.Healthy("SignalR hub registered.", data));
    }
}

/// <summary>
/// SG-05: fails when no active platform-tenant user holds Platform.Tenants.Manage via RolePermissions.
/// No bypass — power must come from seeded RolePermissions.
/// </summary>
public sealed class PlatformOperatorPermissionsHealthCheck(
    IDbConnectionFactory dbFactory,
    ILogger<PlatformOperatorPermissionsHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = dbFactory.CreateConnection();
            var ok = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                SELECT TOP 1 1
                FROM Users u
                INNER JOIN UserRoles ur ON ur.UserId = u.Id
                INNER JOIN Roles r ON r.Id = ur.RoleId
                    AND r.IsActive = 1
                    AND r.TenantId = @PlatformTenantId
                INNER JOIN RolePermissions rp ON rp.RoleId = r.Id
                INNER JOIN Permissions p ON p.Id = rp.PermissionId
                WHERE u.TenantId = @PlatformTenantId
                  AND u.IsActive = 1
                  AND u.IsDeleted = 0
                  AND p.PermissionCode = @Manage
                """,
                new
                {
                    PlatformTenantId = PlatformTenant.Id,
                    Manage = PlatformPermissions.TenantsManage
                },
                cancellationToken: cancellationToken));

            if (ok == 1)
                return HealthCheckResult.Healthy("Platform operator RolePermissions present.");

            logger.LogCritical(
                "SG-05 health check failed: no active user in TenantId={TenantId} holds {Permission} via RolePermissions. See Backend/docs/PLATFORM_OPERATOR_PERMISSIONS_RECOVERY.md.",
                PlatformTenant.Id,
                PlatformPermissions.TenantsManage);

            return HealthCheckResult.Unhealthy(
                $"No active platform-tenant user holds {PlatformPermissions.TenantsManage} via RolePermissions.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "SG-05 platform operator permissions health check threw.");
            return HealthCheckResult.Unhealthy("Platform operator permissions health check failed.", ex);
        }
    }
}
