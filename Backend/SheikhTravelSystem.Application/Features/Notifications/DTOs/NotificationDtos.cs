using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Notifications.DTOs;

public record NotificationDto(
    int Id,
    int? UserId,
    string Title,
    string Message,
    NotificationType Type,
    bool IsRead,
    int? ReferenceId,
    DateTime CreatedAt,
    int Priority = 2,
    string Channel = "InApp",
    string? RecipientType = null,
    bool IsSent = false,
    DateTime? SentDate = null,
    string? TemplateKey = null);

public record NotificationTemplateDto(
    int Id,
    string TemplateKey,
    string TemplateName,
    string Subject,
    string Body,
    string Channel,
    bool IsActive);

public record NotificationDeliveryLogDto(
    int Id,
    int NotificationId,
    string Channel,
    string Status,
    string? Response,
    DateTime CreatedAt);

public record NotificationStatsDto(
    int Unread,
    int Total,
    int Email,
    int Sms,
    int Push,
    int Browser,
    int WhatsApp,
    int Failed);

public record CreateNotificationRequest(
    string Title,
    string Message,
    NotificationType Type = NotificationType.BookingCreated,
    int? UserId = null,
    int? ReferenceId = null,
    int Priority = 2,
    string Channel = "InApp",
    string? RecipientType = null,
    string? TemplateKey = null,
    bool SendNow = true,
    bool Broadcast = false);

public record BulkNotificationRequest(
    string Title,
    string Message,
    NotificationType Type = NotificationType.BookingCreated,
    List<int>? UserIds = null,
    int? ReferenceId = null,
    int Priority = 2,
    List<string>? Channels = null,
    string? TemplateKey = null,
    bool SendNow = true);

public record UpsertNotificationTemplateRequest(
    string TemplateKey,
    string TemplateName,
    string Subject,
    string Body,
    string Channel,
    bool IsActive = true);
