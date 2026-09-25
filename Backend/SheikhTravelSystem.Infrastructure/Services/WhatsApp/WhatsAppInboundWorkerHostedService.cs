using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;

namespace SheikhTravelSystem.Infrastructure.Services.WhatsApp;

/// <summary>
/// Consumes inbound webhook work after the HTTP handler has returned 200.
/// Marks webhook log Succeeded / Failed (SQL DLQ equivalent) after N attempts.
/// </summary>
public sealed class WhatsAppInboundWorkerHostedService(
    IWhatsAppWorkQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<WhatsAppInboundWorkerHostedService> logger) : BackgroundService
{
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WhatsApp inbound worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            WhatsAppInboundWorkItem item;
            try
            {
                item = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IWhatsAppInboxExtended>();
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

            try
            {
                await repo.MarkWebhookLogProcessingAsync(item.WebhookLogId, stoppingToken);
                var result = await mediator.Send(new IngestWhatsAppWebhookCommand(item.Payload), stoppingToken);
                if (result.Success)
                {
                    await repo.MarkWebhookLogSucceededAsync(item.WebhookLogId, stoppingToken);
                }
                else
                {
                    await FailOrRequeueAsync(repo, queue, item, result.Message ?? "Ingest failed", stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WhatsApp inbound worker failed for log {LogId}", item.WebhookLogId);
                try
                {
                    await FailOrRequeueAsync(repo, queue, item, Truncate(ex.Message, 1900), CancellationToken.None);
                }
                catch (Exception markEx)
                {
                    logger.LogError(markEx, "Failed to mark webhook log {LogId}", item.WebhookLogId);
                }
            }
        }

        logger.LogInformation("WhatsApp inbound worker stopped.");
    }

    private static async Task FailOrRequeueAsync(
        IWhatsAppInboxExtended repo,
        IWhatsAppWorkQueue workQueue,
        WhatsAppInboundWorkItem item,
        string error,
        CancellationToken ct)
    {
        var attempts = await repo.IncrementWebhookLogAttemptAsync(item.WebhookLogId, error, ct);
        if (attempts >= MaxAttempts)
        {
            await repo.MarkWebhookLogFailedAsync(item.WebhookLogId, error, ct);
            return;
        }

        // Soft requeue with delay so Meta bursts don't tight-loop.
        await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, attempts * 2)), ct);
        await workQueue.EnqueueInboundAsync(item, ct);
    }

    private static string Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) ? "" : value.Length <= max ? value : value[..max];
}
