namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IWhatsAppRealtimePublisher
{
    Task PublishAsync(int tenantId, object payload, CancellationToken cancellationToken = default);
}
