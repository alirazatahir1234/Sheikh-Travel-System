using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Notifications;

/// <summary>
/// Pulls due multi-channel deliveries with priority-aware polling (Critical faster).
/// Retries use 1m / 5m / 15m via NextRetryAt on the notification row.
/// </summary>
public sealed class NotificationDispatchHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationDispatchHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(30);
            try
            {
                using var scope = scopeFactory.CreateScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notifications.DispatchPendingAsync(50, stoppingToken);

                var priority = await notifications.PeekHighestPendingPriorityAsync(stoppingToken);
                delay = priority switch
                {
                    >= 4 => TimeSpan.FromSeconds(5),
                    3 => TimeSpan.FromSeconds(10),
                    2 => TimeSpan.FromSeconds(30),
                    1 => TimeSpan.FromSeconds(60),
                    _ => TimeSpan.FromSeconds(30)
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Notification dispatch cycle failed");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
