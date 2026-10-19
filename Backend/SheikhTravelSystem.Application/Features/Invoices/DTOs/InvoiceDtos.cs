namespace SheikhTravelSystem.Application.Features.Invoices.DTOs;

public record InvoiceDto(
    int Id,
    int BookingId,
    int CustomerId,
    string InvoiceNumber,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Balance,
    string Status,
    DateTime? IssuedAt,
    string? CustomerName,
    string? BookingNumber,
    DateTime CreatedAt);

public record InvoiceDetailDto(
    int Id,
    int BookingId,
    int CustomerId,
    string InvoiceNumber,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Balance,
    string Status,
    DateTime? IssuedAt,
    string? CustomerName,
    string? BookingNumber,
    string? RouteName,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class InvoiceBookingSnapshot
{
    public int BookingId { get; init; }
    public int CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public string? BookingNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? RouteName { get; init; }
    public int Status { get; init; }
}
