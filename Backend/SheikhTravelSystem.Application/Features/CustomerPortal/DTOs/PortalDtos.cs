using SheikhTravelSystem.Application.Features.Pricing.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

/// <summary>Public catalog row aligned with the admin route list (active routes only).</summary>
public record PortalRouteDto(
    int Id,
    string Label,
    decimal DistanceKm,
    decimal BasePrice,
    string Source,
    string Destination,
    string? Name);

/// <summary>Public catalog row aligned with the admin vehicle list (non-retired only).</summary>
public record PortalVehicleDto(
    int Id,
    string Name,
    string RegistrationNumber,
    int SeatingCapacity,
    decimal FuelAverage,
    string? Model,
    int? Year,
    int FuelType,
    int Status);

public record PortalPriceEstimateRequest(int RouteId, int VehicleId, bool IsRoundTrip = false);

public record CreatePortalBookingRequest(
    string FullName,
    string Phone,
    string? Email,
    int RouteId,
    int VehicleId,
    DateTime PickupTime,
    int PassengerCount,
    bool IsRoundTrip,
    string? Notes,
    PortalPaymentPlan PaymentPlan,
    decimal? InitialPaymentAmount);

public record PortalBookingCreatedDto(
    int BookingId,
    string BookingNumber,
    decimal TotalAmount,
    PriceBreakdown PriceBreakdown,
    PortalPayState PaymentState);

public record PortalBookingCardDto(
    int Id,
    string BookingNumber,
    string RouteLabel,
    DateTime PickupTime,
    BookingStatus BookingStatus,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Remaining,
    PortalPayState PayState);

public record PortalPaymentLineDto(
    int Id,
    decimal Amount,
    PaymentStatus Status,
    DateTime PaymentDate,
    string PaymentMethod);

public record PortalBookingDetailDto(
    int Id,
    string BookingNumber,
    string RouteLabel,
    DateTime PickupTime,
    int PassengerCount,
    string? VehicleName,
    BookingStatus BookingStatus,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Remaining,
    PortalPayState PayState,
    IReadOnlyList<PortalPaymentLineDto> Payments);

public record CreatePortalPaymentRequest(
    string Phone,
    decimal Amount,
    string PaymentMethod,
    string? TransactionReference,
    string? Notes);
