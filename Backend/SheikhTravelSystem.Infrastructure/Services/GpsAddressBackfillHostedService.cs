using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using System.Threading.Channels;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Background reverse-geocode queue. Enqueue is fire-and-forget from position ingest.
/// Only re-resolves when the vehicle has moved ~150 m from the last resolved point
/// (or the current location still has no address).
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

    public void Enqueue(int vehicleId, double latitude, double longitude)
    {
        if (!options.Value.Enabled) return;
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
        using var scope = serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var geocoder = scope.ServiceProvider.GetRequiredService<IReverseGeocodingService>();
        var broadcaster = scope.ServiceProvider.GetRequiredService<ILocationBroadcastService>();

        using var connection = dbFactory.CreateConnection();

        var current = await connection.QuerySingleOrDefaultAsync<(
            string? Address, decimal? Speed, bool? Ignition)?>(
            new CommandDefinition("""
                SELECT Address, Speed, Ignition
                FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId
                """,
                new { VehicleId = vehicleId },
                cancellationToken: cancellationToken));

        // Cache-first resolve (~11 m grid). Nominatim only on miss.
        var result = await geocoder.GetAddressAsync(latitude, longitude, forceRefresh: false, cancellationToken);
        if (result is null || string.IsNullOrWhiteSpace(result.FormattedAddress))
            return;

        if (string.Equals(current?.Address, result.FormattedAddress, StringComparison.Ordinal))
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE VehicleCurrentLocation
            SET Address = @Address
            WHERE VehicleId = @VehicleId
            """,
            new { VehicleId = vehicleId, Address = result.FormattedAddress },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TOP (1) GpsPositions
            SET Address = @Address
            WHERE VehicleId = @VehicleId AND (Address IS NULL OR Address = '')
            """,
            new { VehicleId = vehicleId, Address = result.FormattedAddress },
            cancellationToken: cancellationToken));

        await broadcaster.BroadcastLocationUpdateAsync(
            vehicleId,
            null,
            latitude,
            longitude,
            current?.Speed ?? 0m,
            current?.Ignition,
            DateTime.UtcNow,
            address: result.FormattedAddress,
            cancellationToken: cancellationToken);
    }
}
