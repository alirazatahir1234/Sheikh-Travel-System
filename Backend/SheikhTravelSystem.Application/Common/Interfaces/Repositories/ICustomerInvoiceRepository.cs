using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Invoices.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface ICustomerInvoiceRepository
{
    Task<PagedResult<InvoiceDto>> GetPagedAsync(
        int tenantId,
        int page,
        int pageSize,
        string? status,
        string? paymentStatus,
        int? bookingId,
        int? tripId,
        int? customerId,
        DataScopeResult? scope,
        CancellationToken cancellationToken = default);

    Task<InvoiceDetailDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<InvoiceBookingSnapshot?> GetBookingSnapshotAsync(
        int bookingId, int tenantId, CancellationToken cancellationToken = default);

    Task<InvoiceTripSnapshot?> GetTripSnapshotAsync(
        int tripId, int tenantId, CancellationToken cancellationToken = default);

    Task<decimal> GetTotalPaidForBookingAsync(
        int bookingId, int tenantId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveInvoiceForBookingAsync(
        int bookingId, int tenantId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveInvoiceForTripAsync(
        int tripId, int tenantId, CancellationToken cancellationToken = default);

    Task<string> AllocateInvoiceNumberAsync(
        int tenantId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(
        int tenantId,
        int? createdBy,
        InvoiceCreatePayload payload,
        string invoiceNumber,
        decimal paidAmount,
        decimal balance,
        InvoiceStatus status,
        DateTime issuedAtUtc,
        DateTime dueDateUtc,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task VoidAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task MarkPaidAsync(
        int id, int tenantId, decimal paidAmount, CancellationToken cancellationToken = default);

    Task RecordEmailSentAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task SyncOverdueStatusesAsync(int tenantId, DateTime asOfUtc, CancellationToken cancellationToken = default);

    Task<InvoiceBillingSummaryDto> GetBillingSummaryAsync(
        int tenantId,
        DataScopeResult? scope,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);
}
