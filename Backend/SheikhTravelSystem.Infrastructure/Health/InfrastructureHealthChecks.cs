using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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

        try
        {
            // Lightweight /api/server probe — avoid GetDevicesAsync (full device dump ~1.5s+) on every health hit.
            var server = await traccar.GetServerAsync(cancellationToken);
            var snapshot = syncState.Snapshot(connected: server is not null);
            var data = new Dictionary<string, object>
            {
                ["serverVersion"] = server?.Version ?? "unknown",
                ["lastPositionSyncAt"] = snapshot.LastPositionSyncAt?.ToString("O") ?? "never",
                ["isRunning"] = snapshot.IsRunning
            };
            return server is null
                ? HealthCheckResult.Degraded("Traccar responded without server payload.", data: data)
                : HealthCheckResult.Healthy("Traccar reachable.", data);
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
