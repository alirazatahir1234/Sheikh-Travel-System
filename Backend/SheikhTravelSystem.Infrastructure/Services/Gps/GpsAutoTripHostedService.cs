using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

/// <summary>
/// Keeps daily auto-trips current for tenants in AutoGpsDaily mode (yesterday + today per vehicle).
/// Complements ingest-time persistence when vehicles were offline or ingest was missed.
/// </summary>
public sealed class GpsAutoTripHostedService(
    IServiceProvider serviceProvider,
    ILogger<GpsAutoTripHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var persistence = scope.ServiceProvider.GetRequiredService<IGpsTripPersistenceService>();
                    await persistence.SyncAutoDailyTripsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "GPS auto-trip sync failed.");
                }

                try
                {
                    await Task.Delay(ScanInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
