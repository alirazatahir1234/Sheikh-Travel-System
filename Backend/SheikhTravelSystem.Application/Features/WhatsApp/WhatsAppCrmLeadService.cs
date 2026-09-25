using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp;

public interface IWhatsAppCrmLeadService
{
    /// <summary>
    /// Find customer + lead by phone; create a WebsiteContactRequest only when no lead exists.
    /// Links conversation CustomerId/LeadId. Idempotent.
    /// </summary>
    Task EnsureLeadLinkedAsync(
        int tenantId,
        WhatsAppConversationDto conversation,
        string phoneE164,
        string? profileName,
        CancellationToken cancellationToken = default);
}

public sealed class WhatsAppCrmLeadService(IWhatsAppInboxRepository repository) : IWhatsAppCrmLeadService
{
    public async Task EnsureLeadLinkedAsync(
        int tenantId,
        WhatsAppConversationDto conversation,
        string phoneE164,
        string? profileName,
        CancellationToken cancellationToken = default)
    {
        var customerId = conversation.CustomerId
            ?? await repository.FindCustomerIdByPhoneAsync(tenantId, phoneE164, cancellationToken);

        var leadId = conversation.LeadId
            ?? await repository.FindWebsiteContactRequestIdByPhoneAsync(tenantId, phoneE164, cancellationToken);

        var country = DeriveCountry(phoneE164) ?? conversation.Country;
        var name = profileName ?? conversation.ContactName;

        if (leadId is null)
        {
            leadId = await repository.CreateWhatsAppLeadAsync(new WhatsAppLeadCreateRequest(
                tenantId,
                phoneE164,
                name,
                country,
                conversation.Id,
                conversation.AccountId), cancellationToken);
        }
        else
        {
            await repository.AttachWhatsAppLeadLinksAsync(
                tenantId,
                leadId.Value,
                conversation.Id,
                conversation.AccountId,
                "WhatsApp",
                country,
                name,
                cancellationToken);
        }

        await repository.LinkCustomerAndLeadAsync(
            tenantId, conversation.Id, customerId, leadId, cancellationToken);
    }

    public static string? DeriveCountry(string? phoneE164)
    {
        var digits = WhatsAppPhone.ToApiDigits(phoneE164);
        if (digits.StartsWith("92", StringComparison.Ordinal)) return "Pakistan";
        if (digits.StartsWith("971", StringComparison.Ordinal)) return "United Arab Emirates";
        if (digits.StartsWith("966", StringComparison.Ordinal)) return "Saudi Arabia";
        if (digits.StartsWith("974", StringComparison.Ordinal)) return "Qatar";
        if (digits.StartsWith("968", StringComparison.Ordinal)) return "Oman";
        if (digits.StartsWith("965", StringComparison.Ordinal)) return "Kuwait";
        if (digits.StartsWith("973", StringComparison.Ordinal)) return "Bahrain";
        return null;
    }
}
