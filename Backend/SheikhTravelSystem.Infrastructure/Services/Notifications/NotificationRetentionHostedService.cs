using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Notifications;

/// <summary>Nightly (~02:00 UTC) archive + hard-delete per retention policy.</summary>
public sealed class NotificationRetentionHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationRetentionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);
    private int _lastRunDay = -1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var utc = DateTime.UtcNow;
                if (utc.Hour == 2 && utc.DayOfYear != _lastRunDay)
                {
                    using var scope = scopeFactory.CreateScope();
                    var retention = scope.ServiceProvider.GetRequiredService<INotificationRetentionService>();
                    var result = await retention.RunCleanupAsync(cancellationToken: stoppingToken);
                    _lastRunDay = utc.DayOfYear;
                    logger.LogInformation(
                        "Nightly notification retention finished. Archived={Archive}, Deleted={Delete}, Protected={Protected}",
                        result.EligibleAutoArchive, result.EligibleHardDelete, result.ProtectedCritical);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Notification retention cycle failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
