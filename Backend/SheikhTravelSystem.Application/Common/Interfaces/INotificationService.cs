using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface INotificationService
{
    Task CreateAsync(int? userId, string title, string message, NotificationType type, int? referenceId = null, CancellationToken cancellationToken = default);
    
    Task CreateForAllAsync(string title, string message, NotificationType type, int? referenceId = null, CancellationToken cancellationToken = default);
}
