using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Invoices.Services;

public interface IInvoiceEmailService
{
    Task<ApiResponse<bool>> SendAsync(int invoiceId, CancellationToken cancellationToken = default);
}

public sealed class InvoiceEmailService(
    ICustomerInvoiceRepository invoiceRepository,
    ITenantContext tenantContext,
    INotificationService notifications) : IInvoiceEmailService
{
    public async Task<ApiResponse<bool>> SendAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var invoice = await invoiceRepository.GetByIdAsync(invoiceId, tenantId, cancellationToken);
        if (invoice is null)
            return ApiResponse<bool>.FailResponse("Invoice not found.");

        if (string.IsNullOrWhiteSpace(invoice.CustomerEmail))
            return ApiResponse<bool>.FailResponse("Customer has no email address on file.");

        if (string.Equals(invoice.Status, "Void", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<bool>.FailResponse("Cannot email a void invoice.");

        var tripRef = !string.IsNullOrWhiteSpace(invoice.TripNumber)
            ? invoice.TripNumber
            : invoice.BookingNumber ?? invoice.BookingId?.ToString() ?? "—";

        var body = $"""
            Dear {invoice.CustomerName ?? "Customer"},

            Please find your invoice {invoice.InvoiceNumber} for trip {tripRef}.
            Total: {invoice.TotalAmount:N2}
            Paid: {invoice.PaidAmount:N2}
            Balance due: {invoice.Balance:N2}
            Payment status: {invoice.PaymentStatus}
            Due date: {(invoice.DueDate?.ToString("yyyy-MM-dd") ?? "—")}

            Thank you for choosing SheikhGo.
            """;

        await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
            UserId: null,
            TenantId: tenantId,
            Title: $"Invoice {invoice.InvoiceNumber}",
            Message: body,
            Type: NotificationType.PaymentReceived,
            ReferenceId: invoiceId,
            Priority: 2,
            Channel: NotificationChannels.Email,
            Email: invoice.CustomerEmail.Trim(),
            Module: "Finance",
            TemplateKey: "customer_invoice",
            SendNow: true,
            Variables: new Dictionary<string, string>
            {
                ["InvoiceNumber"] = invoice.InvoiceNumber,
                ["CustomerName"] = invoice.CustomerName ?? "",
                ["TripRef"] = tripRef,
                ["TotalAmount"] = invoice.TotalAmount.ToString("N2"),
                ["Balance"] = invoice.Balance.ToString("N2"),
                ["PaymentStatus"] = invoice.PaymentStatus
            },
            Channels: [NotificationChannels.Email]), cancellationToken);

        await invoiceRepository.RecordEmailSentAsync(invoiceId, tenantId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Invoice emailed to customer.");
    }
}
