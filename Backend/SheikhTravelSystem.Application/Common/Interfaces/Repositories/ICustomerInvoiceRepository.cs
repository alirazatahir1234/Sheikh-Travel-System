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
        int? bookingId,
        int? customerId,
        CancellationToken cancellationToken = default);

    Task<InvoiceDetailDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<InvoiceBookingSnapshot?> GetBookingSnapshotAsync(
        int bookingId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<decimal> GetTotalPaidForBookingAsync(
        int bookingId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveInvoiceForBookingAsync(
        int bookingId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<string> AllocateInvoiceNumberAsync(
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<int> CreateAsync(
        int tenantId,
        int? createdBy,
        InvoiceBookingSnapshot booking,
        string invoiceNumber,
        decimal paidAmount,
        decimal balance,
        InvoiceStatus status,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task VoidAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task MarkPaidAsync(
        int id,
        int tenantId,
        decimal paidAmount,
        CancellationToken cancellationToken = default);
}
