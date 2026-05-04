using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Payments.DTOs;

public record PaymentDto(
    int Id, int BookingId, decimal Amount, string PaymentMethod,
    PaymentStatus Status, DateTime PaymentDate, string? TransactionReference,
    string? Notes, DateTime CreatedAt);

public record PaymentDetailDto(
    int Id, int BookingId, string? BookingNumber, string? CustomerName,
    string? RouteName, decimal Amount, string PaymentMethod,
    PaymentStatus Status, DateTime PaymentDate,
    string? TransactionReference, string? Notes, DateTime CreatedAt,
    decimal TotalBookingAmount);

public record CreatePaymentDto(
    int BookingId, decimal Amount, string PaymentMethod,
    string? TransactionReference, string? Notes);

public record PaymentReportDto(
    decimal TotalReceived, decimal TotalPending, int TotalTransactions,
    List<PaymentDto> RecentPayments);
