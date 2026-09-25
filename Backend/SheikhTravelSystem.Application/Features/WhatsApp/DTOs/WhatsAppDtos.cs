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

public record WhatsAppConversationDto(
    int Id,
    int AccountId,
    string AccountCode,
    int ContactId,
    string ContactPhone,
    string? ContactName,
    int? CustomerId,
    string Status,
    DateTime? LastMessageAt,
    int UnreadCount,
    string? LastMessagePreview,
    int? AssignedUserId = null,
    string? AssignedUserName = null,
    int? LeadId = null,
    string? LeadStatus = null,
    string? CustomerCompany = null,
    bool IsBotEnabled = false,
    string? CurrentBotState = null,
    string? Country = null,
    DateTime? LastIncomingMessageAt = null,
    DateTime? LastOutgoingMessageAt = null,
    bool IsWithinMessagingWindow = false);

public record WhatsAppMessageDto(
    int Id,
    int ConversationId,
    string Direction,
    string? MetaMessageId,
    string Type,
    string? Body,
    string? MediaId,
    string Status,
    DateTime CreatedAtUtc);

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
