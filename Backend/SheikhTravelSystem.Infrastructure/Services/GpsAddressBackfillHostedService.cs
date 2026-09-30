using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Background reverse-geocode queue. Enqueue is fire-and-forget from position ingest.
/// Uses GpsAddressCache first; per-vehicle cooldown prevents Google/Nominatim spam.
/// </summary>
public class GpsAddressBackfillHostedService(
    IServiceProvider serviceProvider,
    IOptions<GeocodingOptions> options,
    ILogger<GpsAddressBackfillHostedService> logger)
    : BackgroundService, IGpsAddressBackfillQueue
{
    private readonly Channel<(int VehicleId, double Latitude, double Longitude)> _queue =
        Channel.CreateBounded<(int, double, double)>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly ConcurrentDictionary<int, DateTime> _lastProcessedUtc = new();
    private readonly ConcurrentDictionary<int, (double Lat, double Lng)> _lastResolvedCoord = new();

    public void Enqueue(int vehicleId, double latitude, double longitude)
    {
        if (!options.Value.Enabled) return;

        var cooldown = TimeSpan.FromSeconds(Math.Max(30, options.Value.BackfillCooldownoldownSeconds));
        if (_lastProcessedUtc.TryGetValue(vehicleId, out var last)
            && DateTime.UtcNow - last < cooldown)
        {
            return;
        }

        _queue.Writer.TryWrite((vehicleId, latitude, longitude));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessAsync(job.VehicleId, job.Latitude, job.Longitude, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "GPS address backfill failed for vehicle {VehicleId}", job.VehicleId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ProcessAsync(int vehicleId, double latitude, double longitude, CancellationToken cancellationToken)
    {
        var cooldown = TimeSpan.FromSeconds(Math.Max(30, options.Value.BackfillCooldownoldownSeconds));
        if (_lastProcessedUtc.TryGetValue(vehicleId, out var last)
            && DateTime.UtcNow - last < cooldown)
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var geocoder = scope.ServiceProvider.GetRequiredService<IReverseGeocodingService>();
        var broadcaster = scope.ServiceProvider.GetRequiredService<ILocationBroadcastService>();

        using var connection = dbFactory.CreateConnection();

        var current = await connection.QuerySingleOrDefaultAsync<(
            string? Address, decimal? Speed, bool? Ignition, double? Latitude, double? Longitude)?>(
            new CommandDefinition("""
                SELECT Address, Speed, Ignition, Latitude, Longitude
                FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId
                """,
                new { VehicleId = vehicleId },
                cancellationToken: cancellationToken));

        // Prefer cache — never forceRefresh on the hot path just because address looks coarse.
        var forceRefresh = false;
        if (TripReplayAddressEnricher.IsCoarseAddress(current?.Address)
            && _lastResolvedCoord.TryGetValue(vehicleId, out var prev)
            && DistanceMeters(prev.Lat, prev.Lng, latitude, longitude) >= 150)
        {
            // Moved enough since last resolve and still admin-only — allow one refresh.
            forceRefresh = true;
        }

        var result = await geocoder.GetAddressAsync(
            latitude,
            longitude,
            forceRefresh,
            cancellationToken,
            allowGoogle: false);
        var formatted = TripReplayAddressEnricher.FormatResolvedAddress(result);
        _lastProcessedUtc[vehicleId] = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(formatted))
            return;

        _lastResolvedCoord[vehicleId] = (latitude, longitude);

        if (string.Equals(current?.Address, formatted, StringComparison.Ordinal))
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE VehicleCurrentLocation
            SET Address = @Address
            WHERE VehicleId = @VehicleId
            """,
            new { VehicleId = vehicleId, Address = formatted },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TOP (1) GpsPositions
            SET Address = @Address
            WHERE VehicleId = @VehicleId
              AND (Address IS NULL OR Address = '' OR Address = @PreviousAddress)
            """,
            new { VehicleId = vehicleId, Address = formatted, PreviousAddress = current?.Address },
            cancellationToken: cancellationToken));

        await broadcaster.BroadcastLocationUpdateAsync(
            vehicleId,
            null,
            latitude,
            longitude,
            current?.Speed ?? 0m,
            current?.Ignition,
            DateTime.UtcNow,
            address: formatted,
            cancellationToken: cancellationToken);
    }

    private static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6371000;
        static double Rad(double d) => d * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2))
                * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * r * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
