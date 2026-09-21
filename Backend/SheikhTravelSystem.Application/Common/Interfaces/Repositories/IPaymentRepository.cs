using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for payments. SQL lives in Infrastructure.
/// </summary>
public interface IPaymentRepository
{
    Task<PagedResult<PaymentDto>> GetPagedAsync(
        int page,
        int pageSize,
        int tenantId,
        int? bookingId,
        PaymentStatus? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        DataScopeResult? scope,
        CancellationToken cancellationToken = default);

    Task<PaymentDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<List<PaymentDto>> GetByBookingIdAsync(int bookingId, CancellationToken cancellationToken = default);

    Task<PaymentReportDto> GetReportAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    Task<PaymentBookingInfo?> GetBookingForPaymentAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task<decimal> GetTotalPaidForBookingAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task<int> CreateAsync(
        CreatePaymentDto dto,
        PaymentStatus status,
        CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        int id,
        PaymentStatus status,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}

public sealed record PaymentBookingInfo(decimal TotalAmount, int Status, string? BookingNumber);
