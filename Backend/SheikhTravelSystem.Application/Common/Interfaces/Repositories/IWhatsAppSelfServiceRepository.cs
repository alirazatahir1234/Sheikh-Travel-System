using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.WhatsApp.Bot;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IWhatsAppSelfServiceRepository
{
    Task<string?> GetBotSessionJsonAsync(int tenantId, int conversationId, CancellationToken ct = default);

    Task SetBotSessionJsonAsync(int tenantId, int conversationId, string? json, CancellationToken ct = default);

    Task<WhatsAppSelfServiceTripSummary?> FindAuthorizedTripOrBookingAsync(
        int tenantId, string phoneE164, string reference, CancellationToken ct = default);

    Task<IReadOnlyList<WhatsAppSelfServiceInvoiceItem>> ListInvoicesForPhoneAsync(
        int tenantId, string phoneE164, int take = 5, CancellationToken ct = default);

    Task<WhatsAppSelfServiceInvoiceItem?> GetAuthorizedInvoiceAsync(
        int tenantId, string phoneE164, int bookingId, CancellationToken ct = default);

    Task<int?> EnsureCustomerIdForPhoneAsync(
        int tenantId, string phoneE164, string? displayName, CancellationToken ct = default);

    Task<int?> GetDefaultRouteIdAsync(int tenantId, CancellationToken ct = default);

    Task<(int VehicleId, string Name)?> FindVehicleByTypeHintAsync(
        int tenantId, string? vehicleTypeHint, CancellationToken ct = default);

    Task<bool> TryInsertFlowSubmissionAsync(
        int tenantId, int conversationId, string idempotencyKey, int? bookingId, CancellationToken ct = default);

    Task<int?> GetBookingIdForFlowSubmissionAsync(
        int tenantId, string idempotencyKey, CancellationToken ct = default);

    Task LinkConversationCustomerAsync(
        int tenantId, int conversationId, int customerId, CancellationToken ct = default);
}
