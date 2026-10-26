using System.Collections.Concurrent;
using System.Globalization;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

/// <summary>
/// Cached Google Roads Speed Limits lookup.
/// Cache order: process memory → SQL <c>RoadSpeedLimitCache</c> → Roads API (when enabled / circuit closed).
/// </summary>
public sealed class RoadSpeedLimitLookupService(
    IDbConnectionFactory dbFactory,
    IGoogleRoadsService roadsService,
    IOptions<RoadSpeedAlertOptions> options,
    ILogger<RoadSpeedLimitLookupService> logger) : IRoadSpeedLimitLookup
{
    private static readonly ConcurrentDictionary<string, (RoadSpeedLimitResult Result, DateTime ExpiresUtc)> MemoryCache = new(StringComparer.Ordinal);
    private static int _consecutiveFailures;
    private static DateTime? _circuitOpenUntilUtc;
    private static readonly object CircuitLock = new();

    public async Task<RoadSpeedLimitResult?> GetSpeedLimitKmhAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var cacheKey = BuildGridCacheKey(latitude, longitude, opts.GridCellDegrees);
        var ttl = TimeSpan.FromHours(Math.Max(1, opts.CacheTtlHours));
        var now = DateTime.UtcNow;

        if (MemoryCache.TryGetValue(cacheKey, out var mem) && mem.ExpiresUtc > now)
            return mem.Result with { FromCache = true };

        var durable = await TryReadDurableAsync(cacheKey, now, cancellationToken);
        if (durable is not null)
        {
            var cached = durable with { FromCache = true };
            MemoryCache[cacheKey] = (cached, now.Add(ttl));
            return cached;
        }

        if (!opts.ApiEnabled || IsCircuitOpen(now))
            return null;

        var api = await roadsService.GetSpeedLimitAsync(latitude, longitude, cancellationToken);
        if (api is null)
        {
            RecordFailure(opts, now);
            return null;
        }

        RecordSuccess();

        var placeKey = !string.IsNullOrWhiteSpace(api.PlaceId)
            ? $"place:{api.PlaceId.Trim()}"
            : cacheKey;

        var result = new RoadSpeedLimitResult(api.SpeedLimitKmh, api.PlaceId, placeKey, FromCache: false);
        var expires = now.Add(ttl);

        MemoryCache[cacheKey] = (result with { FromCache = true }, expires);
        if (!string.Equals(placeKey, cacheKey, StringComparison.Ordinal))
            MemoryCache[placeKey] = (result with { FromCache = true }, expires);

        await TryWriteDurableAsync(cacheKey, placeKey, latitude, longitude, api.SpeedLimitKmh, api.PlaceId, expires, cancellationToken);
        return result;
    }

    internal static string BuildGridCacheKey(double latitude, double longitude, double cellDegrees)
    {
        var cell = cellDegrees <= 0 ? 0.0009 : cellDegrees;
        var latCell = Math.Floor(latitude / cell);
        var lngCell = Math.Floor(longitude / cell);
        return string.Create(CultureInfo.InvariantCulture, $"grid:{latCell}:{lngCell}");
    }

    private async Task<RoadSpeedLimitResult?> TryReadDurableAsync(
        string cacheKey,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            using var connection = dbFactory.CreateConnection();
            var row = await connection.QueryFirstOrDefaultAsync<(decimal SpeedLimitKmh, string? PlaceId, string CacheKey)>(
                new CommandDefinition("""
                    SELECT TOP 1 SpeedLimitKmh, PlaceId, CacheKey
                    FROM RoadSpeedLimitCache
                    WHERE CacheKey = @CacheKey AND ExpiresAtUtc > @Now
                    """,
                    new { CacheKey = cacheKey, Now = nowUtc },
                    cancellationToken: cancellationToken));

            // Dapper returns default tuple when no row — SpeedLimitKmh == 0 and empty CacheKey.
            if (string.IsNullOrEmpty(row.CacheKey) || row.SpeedLimitKmh <= 0)
                return null;

            return new RoadSpeedLimitResult(row.SpeedLimitKmh, row.PlaceId, row.CacheKey, FromCache: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Table may not exist yet during first boot before migration — treat as miss.
            logger.LogDebug(ex, "RoadSpeedLimitCache read skipped");
            return null;
        }
    }

    private async Task TryWriteDurableAsync(
        string gridKey,
        string placeKey,
        double latitude,
        double longitude,
        decimal speedLimitKmh,
        string? placeId,
        DateTime expiresUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            using var connection = dbFactory.CreateConnection();
            await connection.ExecuteAsync(new CommandDefinition("""
                MERGE RoadSpeedLimitCache AS t
                USING (SELECT @CacheKey AS CacheKey) AS s ON t.CacheKey = s.CacheKey
                WHEN MATCHED THEN UPDATE SET
                    PlaceId = @PlaceId,
                    Latitude = @Latitude,
                    Longitude = @Longitude,
                    SpeedLimitKmh = @SpeedLimitKmh,
                    Source = N'google_roads',
                    FetchedAtUtc = SYSUTCDATETIME(),
                    ExpiresAtUtc = @ExpiresAtUtc
                WHEN NOT MATCHED THEN INSERT
                    (CacheKey, PlaceId, Latitude, Longitude, SpeedLimitKmh, Source, FetchedAtUtc, ExpiresAtUtc)
                VALUES
                    (@CacheKey, @PlaceId, @Latitude, @Longitude, @SpeedLimitKmh, N'google_roads', SYSUTCDATETIME(), @ExpiresAtUtc);
                """,
                new
                {
                    CacheKey = gridKey,
                    PlaceId = placeId,
                    Latitude = latitude,
                    Longitude = longitude,
                    SpeedLimitKmh = speedLimitKmh,
                    ExpiresAtUtc = expiresUtc
                },
                cancellationToken: cancellationToken));

            if (!string.Equals(placeKey, gridKey, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(placeId))
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    MERGE RoadSpeedLimitCache AS t
                    USING (SELECT @CacheKey AS CacheKey) AS s ON t.CacheKey = s.CacheKey
                    WHEN MATCHED THEN UPDATE SET
                        PlaceId = @PlaceId,
                        Latitude = @Latitude,
                        Longitude = @Longitude,
                        SpeedLimitKmh = @SpeedLimitKmh,
                        Source = N'google_roads',
                        FetchedAtUtc = SYSUTCDATETIME(),
                        ExpiresAtUtc = @ExpiresAtUtc
                    WHEN NOT MATCHED THEN INSERT
                        (CacheKey, PlaceId, Latitude, Longitude, SpeedLimitKmh, Source, FetchedAtUtc, ExpiresAtUtc)
                    VALUES
                        (@CacheKey, @PlaceId, @Latitude, @Longitude, @SpeedLimitKmh, N'google_roads', SYSUTCDATETIME(), @ExpiresAtUtc);
                    """,
                    new
                    {
                        CacheKey = placeKey,
                        PlaceId = placeId,
                        Latitude = latitude,
                        Longitude = longitude,
                        SpeedLimitKmh = speedLimitKmh,
                        ExpiresAtUtc = expiresUtc
                    },
                    cancellationToken: cancellationToken));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "RoadSpeedLimitCache write skipped");
        }
    }

    private static bool IsCircuitOpen(DateTime nowUtc)
    {
        lock (CircuitLock)
        {
            if (_circuitOpenUntilUtc is { } until && until > nowUtc)
                return true;
            if (_circuitOpenUntilUtc is { } expired && expired <= nowUtc)
            {
                _circuitOpenUntilUtc = null;
                _consecutiveFailures = 0;
            }
            return false;
        }
    }

    private static void RecordFailure(RoadSpeedAlertOptions opts, DateTime nowUtc)
    {
        lock (CircuitLock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= Math.Max(1, opts.CircuitBreakerFailureThreshold))
            {
                _circuitOpenUntilUtc = nowUtc.AddMinutes(Math.Max(1, opts.CircuitBreakerCooldownMinutes));
                _consecutiveFailures = 0;
            }
        }
    }

    private static void RecordSuccess()
    {
        lock (CircuitLock)
        {
            _consecutiveFailures = 0;
            _circuitOpenUntilUtc = null;
        }
    }
}
