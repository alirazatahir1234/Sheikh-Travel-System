using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Infrastructure.Services.WhatsApp;

/// <summary>Every ~15s enqueues due/stuck automation events for recovery and scheduled reminders.</summary>
public sealed class WhatsAppAutomationSchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    IWhatsAppWorkQueue workQueue,
    ILogger<WhatsAppAutomationSchedulerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                using var scope = scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IWhatsAppAutomationRepository>();
                var ids = await repo.ListDueEventIdsAsync(DateTime.UtcNow, 50, stoppingToken);
                foreach (var id in ids)
                    await workQueue.EnqueueAutomationAsync(new WhatsAppAutomationWorkItem(id), stoppingToken);

                if (ids.Count > 0)
                    logger.LogDebug("WhatsApp automation scheduler enqueued {Count} events", ids.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WhatsApp automation scheduler cycle failed");
            }
        }
    }
}
