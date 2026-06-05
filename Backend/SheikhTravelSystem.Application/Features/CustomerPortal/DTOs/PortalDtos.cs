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
    string? Name,
    int? EstimatedDurationMinutes);

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
    int? RouteId,
    int VehicleId,
    DateTime PickupTime,
    int PassengerCount,
    bool IsRoundTrip,
    string? Notes,
    PortalPaymentPlan PaymentPlan,
    decimal? InitialPaymentAmount,
    string? PreferredPaymentMethod = null,
    string? PickupAddress = null,
    string? DropoffAddress = null,
    double? PickupLat = null,
    double? PickupLng = null,
    double? DropLat = null,
    double? DropLng = null,
    decimal? QuotedDistanceKm = null,
    int? QuotedDurationMinutes = null,
    int? AdultCount = null,
    int? ChildCount = null,
    int? LuggageCount = null,
    string? PromoCode = null,
    IReadOnlyList<string>? SeatLabels = null);

public record PortalPointToPointQuoteRequest(
    int VehicleId,
    double PickupLat,
    double PickupLng,
    double DropLat,
    double DropLng,
    bool IsRoundTrip = false,
    int? RouteId = null);

public record PortalQuoteResultDto(
    PriceBreakdown PriceBreakdown,
    decimal DistanceKm,
    int DurationMinutes,
    string? MatchedRouteLabel);

public record PortalValidatePromoRequest(string Code, decimal QuoteTotal);

public record PortalPromoResultDto(bool Valid, string Code, decimal DiscountAmount, string Message);

public record PortalSavedAddressDto(int Id, string Label, string AddressLine, double Latitude, double Longitude);

public record PortalSaveAddressRequest(string Label, string AddressLine, double Latitude, double Longitude);

public record AddPortalFavoriteRouteRequest(int RouteId, string? Label);

public record PortalCustomerNotificationDto(
    int Id,
    string Title,
    string Message,
    string NotificationType,
    int? BookingId,
    bool IsRead,
    DateTime CreatedAt);

public record PortalDriverPreviewDto(
    string? FullName,
    decimal? Rating,
    int? YearsExperience,
    bool IsVerified);

public record PortalSeatLayoutDto(string SeatLabel, int RowIndex, int ColIndex, bool IsBooked);

public record PortalLoyaltyDto(int Points, string Tier);

public record PortalWalletDto(decimal Balance);

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
    IReadOnlyList<PortalPaymentLineDto> Payments,
    string? PickupAddress = null,
    string? DropoffAddress = null,
    PortalDriverPreviewDto? Driver = null,
    IReadOnlyList<string>? Seats = null);

public record CreatePortalPaymentRequest(
    decimal Amount,
    string PaymentMethod,
    string? TransactionReference,
    string? Notes);

public record PortalOtpSentDto(string Phone, bool DevMode, string Message);

public record PortalSendOtpRequest(string Phone);

public record PortalVerifyOtpRequest(string Phone, string Code, string FullName);

public record PortalAuthResultDto(string Phone, string FullName, string AccessToken);

public record PortalBookingTrackingDto(
    int BookingId,
    int? VehicleId,
    string? VehicleName,
    BookingStatus BookingStatus,
    bool TrackingAvailable,
    double? DriverLatitude,
    double? DriverLongitude,
    double? PickupLatitude,
    double? PickupLongitude,
    double? DistanceKm,
    int? EtaMinutes,
    decimal? SpeedKmh = null,
    DateTime? LastUpdatedUtc = null,
    string? DriverPhoneMasked = null);

public record PortalNotificationPreferencesDto(
    bool SmsEnabled,
    bool EmailEnabled,
    string? Email);

public record UpdatePortalNotificationPreferencesRequest(bool SmsEnabled, bool EmailEnabled, string? Email);

public record PortalPaymentGatewayInfoDto(
    bool Enabled,
    string Provider,
    string Message);
