namespace SheikhTravelSystem.Application.Features.Invoices.DTOs;

public record InvoiceDto(
    int Id,
    int? BookingId,
    int? TripId,
    int CustomerId,
    string InvoiceNumber,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Balance,
    string Status,
    string PaymentStatus,
    DateTime? IssuedAt,
    DateTime? DueDate,
    string? CustomerName,
    string? CustomerEmail,
    string? BookingNumber,
    string? TripNumber,
    DateTime CreatedAt,
    DateTime? LastEmailedAt);

public record InvoiceDetailDto(
    int Id,
    int? BookingId,
    int? TripId,
    int CustomerId,
    string InvoiceNumber,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Balance,
    string Status,
    string PaymentStatus,
    DateTime? IssuedAt,
    DateTime? DueDate,
    string? CustomerName,
    string? CustomerEmail,
    string? BookingNumber,
    string? TripNumber,
    string? RouteName,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastEmailedAt,
    int EmailSentCount);

public record InvoiceBillingSummaryDto(
    decimal TotalBilled,
    decimal TotalCollected,
    decimal Outstanding,
    int PendingCount,
    int OverdueCount,
    int PaidCount);

public sealed class InvoiceBookingSnapshot
{
    public int BookingId { get; init; }
    public int CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public string? BookingNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerEmail { get; init; }
    public string? RouteName { get; init; }
    public int Status { get; init; }
}

public sealed class InvoiceTripSnapshot
{
    public int TripId { get; init; }
    public int? BookingId { get; init; }
    public int CustomerId { get; init; }
    public int? VehicleId { get; init; }
    public string? TripNumber { get; init; }
    public string? BookingNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerEmail { get; init; }
    public string? RouteName { get; init; }
    public decimal TotalAmount { get; init; }
    public int Status { get; init; }
}

public sealed class InvoiceCreatePayload
{
    public int? BookingId { get; init; }
    public int? TripId { get; init; }
    public int CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerEmail { get; init; }
    public string? BookingNumber { get; init; }
    public string? TripNumber { get; init; }
    public string? RouteName { get; init; }
}
