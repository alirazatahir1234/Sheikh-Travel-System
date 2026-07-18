using SheikhTravelSystem.Application.Features.Notifications.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface INotificationRetentionService
{
    /// <summary>Auto-archive eligible read items and hard-delete expired soft-deleted/archived rows.</summary>
    Task<NotificationRetentionEstimateDto> RunCleanupAsync(int? tenantId = null, CancellationToken cancellationToken = default);
}
