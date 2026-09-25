namespace SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

/// <summary>Safe WhatsApp account projection — never includes access tokens.</summary>
public record WhatsAppAccountDto(
    int Id,
    string Code,
    string DisplayName,
    string E164Phone,
    string Purpose,
    string? PhoneNumberId,
    bool IsActive,
    bool HasAccessToken,
    string? Country = null,
    string? CountryCode = null,
    string Status = "Active",
    bool IsDefault = false,
    DateTime? LastHealthCheckedAtUtc = null,
    string? LastHealthStatus = null,
    string? LastHealthMessage = null);

public record WhatsAppAccountHealthResultDto(
    int AccountId,
    string Status,
    string? Message,
    DateTime CheckedAtUtc);

public record WhatsAppConversationDto
{
    public int Id { get; init; }
    public int AccountId { get; init; }
    public string AccountCode { get; init; } = "";
    public int ContactId { get; init; }
    public string ContactPhone { get; init; } = "";
    public string? ContactName { get; init; }
    public int? CustomerId { get; init; }
    public string Status { get; init; } = "";
    public DateTime? LastMessageAt { get; init; }
    public int UnreadCount { get; init; }
    public string? LastMessagePreview { get; init; }
    public int? AssignedUserId { get; init; }
    public string? AssignedUserName { get; init; }
    public int? LeadId { get; init; }
    public string? LeadStatus { get; init; }
    public string? CustomerCompany { get; init; }
    public bool IsBotEnabled { get; init; }
    public string? CurrentBotState { get; init; }
    public string? Country { get; init; }
    public DateTime? LastIncomingMessageAt { get; init; }
    public DateTime? LastOutgoingMessageAt { get; init; }
    public DateTime? WindowExpiresAt { get; init; }
    public bool IsWithinMessagingWindow { get; init; }
}

public record WhatsAppMessageDto(
    int Id,
    int ConversationId,
    string Direction,
    string? MetaMessageId,
    string Type,
    string? Body,
    string? MediaId,
    string Status,
    DateTime CreatedAtUtc,
    int AttemptCount = 1,
    string? ErrorMessage = null,
    string? ErrorCode = null,
    long? AutomationEventId = null,
    int? BookingId = null);

public record WhatsAppWebhookLogDto(
    long Id,
    DateTime ReceivedAtUtc,
    bool SignatureValid,
    string ProcessingStatus,
    string? Error,
    int Attempts,
    DateTime? ProcessedAtUtc,
    string? PhoneNumberId,
    int? TenantId,
    string? PayloadPreview);

public record WhatsAppConversationContextDto(
    int ConversationId,
    string ContactPhone,
    string? ContactName,
    int? CustomerId,
    string? CustomerName,
    string? CustomerCompany,
    int? LeadId,
    string? LeadStatus,
    IReadOnlyList<WhatsAppContextBookingDto> ActiveBookings,
    WhatsAppContextTripDto? LastCompletedTrip,
    decimal UnpaidInvoiceTotal);

public record WhatsAppContextBookingDto(
    int Id,
    string BookingNumber,
    string Status,
    DateTime? TravelDate);

public record WhatsAppContextTripDto(
    int Id,
    string? TripNumber,
    string Status,
    DateTime? CompletedAt);

public record WhatsAppContactDto(
    int Id,
    int AccountId,
    string WaId,
    string PhoneE164,
    string? ProfileName,
    int? CustomerId,
    string? SuggestedCustomerName);

public record SendWhatsAppMessageResultDto(
    int MessageId,
    string? MetaMessageId,
    string Status);

public record WhatsAppMediaProxyResult(
    byte[] Bytes,
    string ContentType);

public record WhatsAppAutomationRuleDto(
    int Id,
    int TenantId,
    string EventType,
    bool IsEnabled,
    int? AccountId,
    string? TemplateName,
    int OffsetMinutes,
    bool NotifyBooker,
    bool IsUrgent);

public record WhatsAppAutomationTimelineItemDto(
    long Id,
    string EventType,
    byte Status,
    string? SkipReason,
    DateTime DueAt,
    DateTime? ProcessedAt,
    string? RecipientPhone,
    int? MessageId,
    string? MessageStatus,
    DateTime? MessageCreatedAt);

public record WhatsAppConsentDto(
    int Id,
    string WaId,
    DateTime? OptInAt,
    string? OptInSource,
    DateTime? OptOutAt,
    string? Note);

public record TrackingViewDto(
    string State,
    string? BookingNo,
    string? DriverFirstName,
    TrackingVehicleDto? Vehicle,
    TrackingPlaceDto? Pickup,
    TrackingPlaceDto? Dropoff,
    TrackingPositionDto? Position,
    int? EtaMinutes,
    int RefreshSeconds,
    TrackingBrandDto Brand);

public record TrackingVehicleDto(string Description, string Plate);
public record TrackingPlaceDto(string? Address, double? Lat, double? Lng, DateTime? At);
public record TrackingPositionDto(double Lat, double Lng, double? HeadingDeg, double? SpeedKmh, DateTime FixTime);
public record TrackingBrandDto(string Name, string? SupportPhone);

public record AutomationPreviewDto(
    string EventType,
    string TemplateName,
    string Language,
    string RecipientPhone,
    IReadOnlyList<string> BodyParameters,
    string? BodyPreview,
    string? TrackingUrl);
