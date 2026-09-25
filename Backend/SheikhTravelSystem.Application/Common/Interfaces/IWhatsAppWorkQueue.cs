namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Abstraction for WhatsApp work. In-process Channel today; RabbitMQ adapter later.
/// Lanes: inbound (webhook) and automation (trip events).
/// </summary>
public interface IWhatsAppWorkQueue
{
    ValueTask EnqueueInboundAsync(WhatsAppInboundWorkItem item, CancellationToken cancellationToken = default);

    ValueTask EnqueueAutomationAsync(WhatsAppAutomationWorkItem item, CancellationToken cancellationToken = default);

    /// <summary>Blocks until an inbound item is available.</summary>
    ValueTask<WhatsAppInboundWorkItem> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>Blocks until an automation item is available.</summary>
    ValueTask<WhatsAppAutomationWorkItem> DequeueAutomationAsync(CancellationToken cancellationToken = default);
}

public sealed record WhatsAppInboundWorkItem(
    long WebhookLogId,
    string Payload,
    bool SignatureValid);

public sealed record WhatsAppAutomationWorkItem(long AutomationEventId);
