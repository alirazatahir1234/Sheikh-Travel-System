using System.Threading.Channels;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.WhatsApp;

/// <summary>In-process Channel-backed queue with inbound + automation lanes.</summary>
public sealed class ChannelWhatsAppWorkQueue : IWhatsAppWorkQueue
{
    private readonly Channel<WhatsAppInboundWorkItem> _inbound = Channel.CreateUnbounded<WhatsAppInboundWorkItem>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private readonly Channel<WhatsAppAutomationWorkItem> _automation = Channel.CreateUnbounded<WhatsAppAutomationWorkItem>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    public ValueTask EnqueueInboundAsync(WhatsAppInboundWorkItem item, CancellationToken cancellationToken = default)
        => _inbound.Writer.WriteAsync(item, cancellationToken);

    public ValueTask EnqueueAutomationAsync(WhatsAppAutomationWorkItem item, CancellationToken cancellationToken = default)
        => _automation.Writer.WriteAsync(item, cancellationToken);

    public ValueTask<WhatsAppInboundWorkItem> DequeueAsync(CancellationToken cancellationToken = default)
        => _inbound.Reader.ReadAsync(cancellationToken);

    public ValueTask<WhatsAppAutomationWorkItem> DequeueAutomationAsync(CancellationToken cancellationToken = default)
        => _automation.Reader.ReadAsync(cancellationToken);
}
