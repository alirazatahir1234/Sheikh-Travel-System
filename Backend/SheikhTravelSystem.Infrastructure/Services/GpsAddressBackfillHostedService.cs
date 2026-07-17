using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Backfills human-readable addresses for GPS positions that arrive without one (Traccar's own
/// geocoder is unconfigured on our server as of writing, so it always sends address: null). Queries
/// the free Nominatim public API, which caps requests at 1/sec and requires a real User-Agent — both
/// enforced here. A DB-backed cache keyed by ~11m-rounded coordinates means only genuinely new
/// locations ever hit the network; parked/idle vehicles and repeat routes resolve from cache
/// instantly. Enqueue is fire-and-forget from the position-ingest path so a cache miss never blocks
/// a live GPS ping — the resolved address lands in VehicleCurrentLocation a few seconds later, well
/// in time for the next live-map poll.
/// </summary>
public class GpsAddressBackfillHostedService(
    IServiceProvider serviceProvider,
    IHttpClientFactory httpClientFactory,
    IOptions<GeocodingOptions> options,
    ILogger<GpsAddressBackfillHostedService> logger)
    : BackgroundService, IGpsAddressBackfillQueue
{
    // ~11m grid at the equator — street-level precision with a good cache-hit rate for idle/parked
    // vehicles and slow-moving traffic between consecutive pings.
    private const int CoordinateDecimals = 4;

    // Nominatim's usage policy hard-caps public API usage at 1 request/sec.
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromSeconds(1);

    private readonly Channel<(int VehicleId, double Latitude, double Longitude)> _queue =
        Channel.CreateBounded<(int, double, double)>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private DateTime _nextAllowedCallUtc = DateTime.MinValue;

    public void Enqueue(int vehicleId, double latitude, double longitude)
    {
        if (!options.Value.Enabled)
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
        using var scope = serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var connection = dbFactory.CreateConnection();

        var latKey = Math.Round(latitude, CoordinateDecimals, MidpointRounding.AwayFromZero);
        var lngKey = Math.Round(longitude, CoordinateDecimals, MidpointRounding.AwayFromZero);

        var address = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT TOP 1 Address FROM GpsAddressCache WHERE LatitudeKey = @LatKey AND LongitudeKey = @LngKey",
            new { LatKey = latKey, LngKey = lngKey },
            cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(address))
        {
            address = await ResolveFromNominatimAsync(latitude, longitude, cancellationToken);
            if (string.IsNullOrWhiteSpace(address))
            {
                return;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                MERGE GpsAddressCache AS target
                USING (SELECT @LatKey AS LatitudeKey, @LngKey AS LongitudeKey) AS source
                ON target.LatitudeKey = source.LatitudeKey AND target.LongitudeKey = source.LongitudeKey
                WHEN MATCHED THEN UPDATE SET Address = @Address, ResolvedAt = @ResolvedAt
                WHEN NOT MATCHED THEN INSERT (LatitudeKey, LongitudeKey, Address, ResolvedAt)
                    VALUES (@LatKey, @LngKey, @Address, @ResolvedAt);
                """,
                new { LatKey = latKey, LngKey = lngKey, Address = address, ResolvedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));
        }

        // Only fill a gap — never clobber a fresher address a normal ingest may have set meanwhile.
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE VehicleCurrentLocation SET Address = @Address WHERE VehicleId = @VehicleId AND (Address IS NULL OR Address = '')",
            new { VehicleId = vehicleId, Address = address },
            cancellationToken: cancellationToken));
    }

    private async Task<string?> ResolveFromNominatimAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var waitMs = (_nextAllowedCallUtc - DateTime.UtcNow).TotalMilliseconds;
        if (waitMs > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(waitMs), cancellationToken);
        }

        _nextAllowedCallUtc = DateTime.UtcNow.Add(ThrottleInterval);

        var client = httpClientFactory.CreateClient("Nominatim");
        var url =
            $"/reverse?format=jsonv2&lat={latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}&zoom=18&addressdetails=0&accept-language=en";

        try
        {
            var response = await client.GetFromJsonAsync<NominatimReverseResponse>(url, cancellationToken);
            return response?.DisplayName;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Nominatim reverse geocode failed for {Latitude},{Longitude}", latitude, longitude);
            return null;
        }
    }

    private sealed record NominatimReverseResponse(
        [property: JsonPropertyName("display_name")] string? DisplayName);
}
