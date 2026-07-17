using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public record NotificationCreateOptions(
    int? UserId,
    string Title,
    string Message,
    NotificationType Type,
    int? ReferenceId = null,
    int Priority = 2,
    string Channel = "InApp",
    string? RecipientType = null,
    string? TemplateKey = null,
    bool SendNow = true,
    string? Email = null,
    string? Phone = null);

public record ChannelSendRequest(
    int NotificationId,
    int? UserId,
    string Title,
    string Message,
    string Channel,
    string? Email = null,
    string? Phone = null,
    string? TemplateKey = null);

public record ChannelSendResult(bool Success, string Status, string? Response);

public interface INotificationChannelSender
{
    string Channel { get; }
    Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default);
}

public interface INotificationRealtimePublisher
{
    Task PublishToUserAsync(int userId, object payload, CancellationToken cancellationToken = default);
    Task PublishToDispatchersAsync(object payload, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task CreateAsync(
        int? userId,
        string title,
        string message,
        NotificationType type,
        int? referenceId = null,
        CancellationToken cancellationToken = default);

    Task CreateForAllAsync(
        string title,
        string message,
        NotificationType type,
        int? referenceId = null,
        CancellationToken cancellationToken = default);

    Task<int> CreateAndDispatchAsync(NotificationCreateOptions options, CancellationToken cancellationToken = default);

    Task DispatchByIdAsync(int notificationId, CancellationToken cancellationToken = default);

    Task DispatchPendingAsync(int maxBatch = 50, CancellationToken cancellationToken = default);
}
