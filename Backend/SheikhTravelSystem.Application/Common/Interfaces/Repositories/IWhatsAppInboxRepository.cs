using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IWhatsAppInboxRepository
{
    Task<IReadOnlyList<WhatsAppAccountDto>> GetAccountsAsync(
        int tenantId,
        bool includeInactive = false,
        CancellationToken ct = default);

    Task<bool> SetAccountActiveAsync(
        int tenantId,
        int accountId,
        bool isActive,
        CancellationToken ct = default);

    Task UpdateAccountHealthAsync(
        int tenantId,
        int accountId,
        string status,
        string? message,
        DateTime checkedAtUtc,
        CancellationToken ct = default);

    Task<IReadOnlyList<WhatsAppAccountRow>> ListActiveAccountRowsAsync(CancellationToken ct = default);

    Task<WhatsAppAccountRow?> GetAccountByIdAsync(int tenantId, int accountId, CancellationToken ct = default);

    Task<WhatsAppAccountRow?> GetAccountByPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct = default);

    Task<(IReadOnlyList<WhatsAppConversationDto> Items, int Total)> GetConversationsAsync(
        int tenantId,
        int? accountId,
        string? search,
        string? filter,
        int? currentUserId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<WhatsAppConversationDto?> GetConversationAsync(int tenantId, int conversationId, CancellationToken ct = default);

    Task<(IReadOnlyList<WhatsAppMessageDto> Items, int Total)> GetMessagesAsync(
        int tenantId,
        int conversationId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task MarkConversationReadAsync(int tenantId, int conversationId, CancellationToken ct = default);

    Task<bool> SetConversationAssignmentAsync(
        int tenantId, int conversationId, int? assignedUserId, CancellationToken ct = default);

    Task<bool> SetConversationStatusAsync(
        int tenantId, int conversationId, string status, CancellationToken ct = default);

    Task<bool> SetConversationBotAsync(
        int tenantId, int conversationId, bool isBotEnabled, string? currentBotState, CancellationToken ct = default);

    Task<int> UpsertInboundMessageAsync(WhatsAppInboundPersistRequest request, CancellationToken ct = default);

    Task<WhatsAppMessageStatusUpdate?> UpdateMessageStatusAsync(
        string messageId,
        string status,
        string? errorMessage = null,
        CancellationToken ct = default);

    Task<bool> MessageExistsAsync(string messageId, CancellationToken ct = default);

    Task<int> InsertOutboundMessageAsync(
        int tenantId,
        int conversationId,
        string? messageId,
        string type,
        string? body,
        string status,
        CancellationToken ct = default,
        string? templateName = null);

    Task SetOutboundMetaIdAsync(int messageId, string metaMessageId, string status, CancellationToken ct = default);

    Task SetOutboundStatusAsync(int messageId, string status, string? errorMessage = null, CancellationToken ct = default);

    /// <summary>Last inbound customer message time for messaging-window checks (null if no conversation).</summary>
    Task<DateTime?> GetLastIncomingMessageAtAsync(
        int tenantId, int accountId, string customerPhoneNumber, CancellationToken ct = default);

    /// <summary>Find or create an open conversation for account + customer phone.</summary>
    Task<int> EnsureConversationAsync(
        int tenantId,
        int accountId,
        string customerPhoneNumber,
        string? customerName = null,
        CancellationToken ct = default);

    /// <summary>Resolves conversation identity for CRM linking (id is conversation id).</summary>
    Task<WhatsAppContactDto?> GetContactAsync(int tenantId, int conversationId, CancellationToken ct = default);

    Task LinkCustomerAsync(int tenantId, int conversationId, int? customerId, CancellationToken ct = default);

    Task<int?> FindCustomerIdByPhoneAsync(int tenantId, string phoneE164, CancellationToken ct = default);

    /// <summary>Links an existing website contact request by phone; prefers active lead statuses.</summary>
    Task<int?> FindWebsiteContactRequestIdByPhoneAsync(int tenantId, string phoneE164, CancellationToken ct = default);

    Task LinkLeadAsync(int tenantId, int conversationId, int? leadId, CancellationToken ct = default);

    Task LinkCustomerAndLeadAsync(
        int tenantId,
        int conversationId,
        int? customerId,
        int? leadId,
        CancellationToken ct = default);

    Task<int> CreateWhatsAppLeadAsync(WhatsAppLeadCreateRequest request, CancellationToken ct = default);

    Task UpdateWhatsAppLeadQualificationAsync(WhatsAppLeadQualificationUpdate request, CancellationToken ct = default);

    Task AttachWhatsAppLeadLinksAsync(
        int tenantId,
        int leadId,
        int conversationId,
        int accountId,
        string? sourceIfEmpty = "WhatsApp",
        string? country = null,
        string? customerName = null,
        CancellationToken ct = default);

    Task InsertWebhookDeadLetterAsync(string reason, string? payloadPreview, CancellationToken ct = default);

    Task DeleteOldMessagesAsync(int retentionDays, CancellationToken ct = default);
}

public sealed record WhatsAppAccountRow(
    int Id,
    int TenantId,
    string Code,
    string Name,
    string DisplayPhoneNumber,
    string Purpose,
    string? PhoneNumberId,
    string? BusinessAccountId,
    bool IsActive,
    bool IsDefault = false);

public sealed record WhatsAppInboundPersistRequest(
    int TenantId,
    int AccountId,
    string WaId,
    string PhoneE164,
    string? ProfileName,
    string MessageId,
    string Type,
    string? Body,
    string? MediaId,
    string? RawPayload);

public sealed record WhatsAppLeadCreateRequest(
    int TenantId,
    string PhoneE164,
    string? CustomerName,
    string? Country,
    int ConversationId,
    int AccountId);

public sealed record WhatsAppLeadQualificationUpdate(
    int TenantId,
    int LeadId,
    string? FleetType = null,
    string? FleetSize = null,
    string? MainChallenge = null,
    string? Status = null,
    string? Message = null);

public sealed record WhatsAppMessageStatusUpdate(
    int TenantId,
    int ConversationId,
    int MessageDbId,
    string MetaMessageId,
    string Status);
