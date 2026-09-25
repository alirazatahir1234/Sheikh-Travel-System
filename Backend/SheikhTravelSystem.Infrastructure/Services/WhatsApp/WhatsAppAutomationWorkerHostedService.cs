using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;

namespace SheikhTravelSystem.Infrastructure.Services.WhatsApp;

public sealed class WhatsAppAutomationWorkerHostedService(
    IWhatsAppWorkQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<WhatsAppAutomationWorkerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WhatsApp automation worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            WhatsAppAutomationWorkItem item;
            try
            {
                item = await queue.DequeueAutomationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
                await mediator.Send(new ProcessAutomationEventCommand(item.AutomationEventId), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Automation worker failed for event {EventId}", item.AutomationEventId);
            }
        }

        logger.LogInformation("WhatsApp automation worker stopped.");
    }
}
