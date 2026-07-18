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
    string? TemplateKey = null,
    string? Module = null,
    DateTime? ReadDate = null,
    string? DeliveryStatus = null,
    bool IsArchived = false,
    bool IsDeleted = false,
    string? RetentionCategory = null,
    bool NeverAutoDelete = false);

public record NotificationTemplateDto(
    int Id,
    string TemplateKey,
    string TemplateName,
    string Subject,
    string Body,
    string Channel,
    bool IsActive,
    string Language = "en",
    string? Variables = null);

public record NotificationDeliveryLogDto(
    int Id,
    int NotificationId,
    string Channel,
    string Status,
    string? Response,
    DateTime CreatedAt,
    string? Provider = null,
    int RetryCount = 0,
    DateTime? NextRetryAt = null);

public record NotificationStatsDto(
    int Unread,
    int Total,
    int Email,
    int Sms,
    int Push,
    int Browser,
    int WhatsApp,
    int Failed);

public record NotificationPreferencesDto(
    bool EmailEnabled = true,
    bool SmsEnabled = true,
    bool PushEnabled = true,
    bool BrowserEnabled = true,
    bool WhatsAppEnabled = false);

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
    bool Broadcast = false,
    string? Module = null,
    List<string>? Channels = null);

public record BulkNotificationRequest(
    string Title,
    string Message,
    NotificationType Type = NotificationType.BookingCreated,
    List<int>? UserIds = null,
    int? ReferenceId = null,
    int Priority = 2,
    List<string>? Channels = null,
    string? TemplateKey = null,
    bool SendNow = true,
    string? Module = null);

/// <summary>Admin compose — manual email / multi-channel message from Notification Center.</summary>
public record SendManualMessageRequest(
    string Subject,
    string Body,
    int Priority = 2,
    List<int>? RecipientUserIds = null,
    List<string>? EmailAddresses = null,
    string? Role = null,
    List<string>? Channels = null,
    string? TemplateKey = null,
    bool SendNow = true);

public record NotificationRetentionDto(
    int ReadArchiveDays = 30,
    int ArchivedDeleteDays = 180,
    int FailedDeleteDays = 90,
    int DraftDeleteDays = 30,
    int OperationalDeleteDays = 90,
    int MaintenanceDeleteDays = 730,
    int ComplianceDeleteDays = 2555,
    bool CriticalNeverDelete = true,
    int SecurityDeleteDays = 730);

public record NotificationRetentionEstimateDto(
    int EligibleAutoArchive,
    int EligibleHardDelete,
    int ProtectedCritical);

public record NotificationLifecycleIdsRequest(List<int>? Ids = null);

public sealed class NotificationRecipientDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
}

public record UpsertNotificationTemplateRequest(
    string TemplateKey,
    string TemplateName,
    string Subject,
    string Body,
    string Channel,
    bool IsActive = true,
    string Language = "en",
    string? Variables = null);
