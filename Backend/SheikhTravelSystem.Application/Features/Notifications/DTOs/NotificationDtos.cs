using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Notifications.DTOs;

public record NotificationDto(int Id, int? UserId, string Title, string Message, NotificationType Type, bool IsRead, int? ReferenceId, DateTime CreatedAt);
