using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Infrastructure.Traccar;

public class TraccarSyncService(
    IServiceScopeFactory scopeFactory,
    IOptions<TraccarOptions> options,
    ILogger<TraccarSyncService> logger) : BackgroundService
{
    private DateTime _lastPositionSync = DateTime.MinValue;
    private DateTime _lastEventSync = DateTime.MinValue;
    private DateTime _lastDeviceSync = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;

        if (!opts.Enabled)
        {
            logger.LogInformation("Traccar sync is disabled (Traccar:Enabled = false). Background service will not poll.");
            return;
        }

        if (!opts.IsConfigured)
        {
            logger.LogWarning(
                "Traccar sync is enabled but Traccar:BaseUrl is missing or invalid. " +
                "Set Traccar:BaseUrl (e.g. http://20.174.1.230:8082) in user secrets or environment.");
            return;
        }

        var positionInterval = TimeSpan.FromSeconds(Math.Max(1, opts.ResolvedPositionIntervalSeconds));
        var eventInterval = TimeSpan.FromSeconds(Math.Max(1, opts.EventSyncIntervalSeconds));
        var deviceInterval = TimeSpan.FromSeconds(Math.Max(60, opts.DeviceSyncIntervalSeconds));

        logger.LogInformation(
            "Traccar sync scheduler started from {Url}. Intervals: positions {Pos}s, events {Evt}s, devices {Dev}s",
            opts.BaseUrl,
            positionInterval.TotalSeconds,
            eventInterval.TotalSeconds,
            deviceInterval.TotalSeconds);

        try
        {
            await RunJobAsync(o => o.SyncDevicesAsync(stoppingToken), stoppingToken);
            _lastDeviceSync = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Initial Traccar device sync on startup failed — will retry on schedule");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            try
            {
                if (now - _lastPositionSync >= positionInterval)
                {
                    await RunJobAsync(o => o.SyncPositionsAsync(stoppingToken), stoppingToken);
                    _lastPositionSync = DateTime.UtcNow;
                }

                if (now - _lastEventSync >= eventInterval)
                {
                    await RunJobAsync(o => o.SyncEventsAsync(stoppingToken), stoppingToken);
                    _lastEventSync = DateTime.UtcNow;
                }

                if (now - _lastDeviceSync >= deviceInterval)
                {
                    await RunJobAsync(o => o.SyncDevicesAsync(stoppingToken), stoppingToken);
                    _lastDeviceSync = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Traccar scheduler tick failed — will retry");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        logger.LogInformation("Traccar sync scheduler stopped.");
    }

    private async Task RunJobAsync(
        Func<ITraccarSyncOrchestrator, Task<TraccarSyncRunResult>> job,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ITraccarSyncOrchestrator>();
        await job(orchestrator);
    }
}
