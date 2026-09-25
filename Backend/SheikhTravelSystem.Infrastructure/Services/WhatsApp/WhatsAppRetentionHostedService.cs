using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;

namespace SheikhTravelSystem.Infrastructure.Services.WhatsApp;

public sealed class WhatsAppRetentionHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppRetentionHostedService> logger) : BackgroundService
{
    private const int WebhookLogRetentionDays = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
                var days = Math.Max(30, options.Value.MessageRetentionDays);
                using var scope = scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IWhatsAppInboxRepository>();
                var inboxExtended = scope.ServiceProvider.GetRequiredService<IWhatsAppInboxExtended>();
                await repo.DeleteOldMessagesAsync(days, stoppingToken);
                await inboxExtended.DeleteOldWebhookLogsAsync(WebhookLogRetentionDays, stoppingToken);
                logger.LogInformation(
                    "WhatsApp retention completed. MessageDays={Days} WebhookLogDays={WebhookDays}",
                    days, WebhookLogRetentionDays);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WhatsApp retention cycle failed");
            }
        }
    }
}
