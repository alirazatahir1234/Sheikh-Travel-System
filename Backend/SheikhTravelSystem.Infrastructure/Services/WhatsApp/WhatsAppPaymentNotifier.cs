using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using Microsoft.Extensions.Logging;
using Dapper;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Services.WhatsApp;

public interface IWhatsAppPaymentNotifier
{
    Task NotifyPaymentReceivedAsync(
        int bookingId, decimal amount, string paymentMethod, CancellationToken cancellationToken = default);
}

public sealed class WhatsAppPaymentNotifier(
    IDbConnectionFactory dbFactory,
    IWhatsAppInboxRepository inbox,
    IWhatsAppCloudApiService cloudApi,
    IWhatsAppAccountConfig accountConfig,
    IWhatsAppAccountResolver accountResolver,
    ILogger<WhatsAppPaymentNotifier> logger) : IWhatsAppPaymentNotifier
{
    public async Task NotifyPaymentReceivedAsync(
        int bookingId, decimal amount, string paymentMethod, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = dbFactory.CreateConnection();
            var row = await connection.QuerySingleOrDefaultAsync<(
                int TenantId, string? BookingNumber, string? Phone)>(new CommandDefinition("""
                SELECT b.TenantId, b.BookingNumber, c.Phone
                FROM Bookings b
                INNER JOIN Customers c ON c.Id = b.CustomerId
                WHERE b.Id = @BookingId AND b.IsDeleted = 0
                """, new { BookingId = bookingId }, cancellationToken: cancellationToken));

            if (row.TenantId == 0 || string.IsNullOrWhiteSpace(row.Phone))
                return;

            var account = await accountResolver.ResolveAsync(row.TenantId, null, row.Phone, cancellationToken);
            if (account is null || accountConfig.ResolveCredentials(account) is null)
                return;

            var conversationId = await inbox.EnsureConversationAsync(
                row.TenantId, account.Id, WhatsAppPhone.ToE164(row.Phone) ?? row.Phone, null, cancellationToken);

            var body =
                $"Payment received\n\nInvoice: {row.BookingNumber}\nAmount: PKR {amount:0.##}\nStatus: Paid\nThank you for your payment.";

            var pendingId = await inbox.InsertOutboundMessageAsync(
                row.TenantId, conversationId, null, "text", body, "Queued", cancellationToken);
            await inbox.SetOutboundStatusAsync(pendingId, "Sending", null, cancellationToken);

            var send = await cloudApi.SendTextAsync(
                account, WhatsAppPhone.ToApiDigits(row.Phone), body, cancellationToken);

            if (!send.Success)
            {
                await inbox.SetOutboundMetaIdAsync(
                    pendingId, send.MetaMessageId ?? $"fail-{pendingId}", "Failed", cancellationToken);
                await inbox.SetOutboundStatusAsync(pendingId, "Failed", send.ErrorMessage, cancellationToken);
                return;
            }

            await inbox.SetOutboundMetaIdAsync(pendingId, send.MetaMessageId!, "Sent", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WhatsApp payment notification failed for booking {BookingId}", bookingId);
        }
    }
}
