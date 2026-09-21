using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Application.Features.CustomerPortal.Queries;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for customer portal. SQL lives in Infrastructure.
/// </summary>
public interface ICustomerPortalRepository
{
    // ── Booking access / customer identity ──────────────────────────────────
    Task<IReadOnlyList<int>> ResolvePortalCustomerIdsAsync(
        string phone, int? jwtCustomerId, CancellationToken cancellationToken = default);

    Task<bool> CustomerOwnsBookingAsync(
        int bookingId, string phone, int? customerId, CancellationToken cancellationToken = default);

    Task NormalizeCustomerPhonesAsync(CancellationToken cancellationToken = default);

    Task<int?> ResolveCustomerIdByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    Task<int> EnsureCustomerAsync(string phone, string fullName, int tenantId, CancellationToken cancellationToken = default);

    Task WriteCustomerNotificationAsync(
        int customerId, string title, string message, string type, int? bookingId,
        CancellationToken cancellationToken = default);

    Task EnsureLoyaltyRowAsync(int customerId, CancellationToken cancellationToken = default);

    Task AddLoyaltyPointsAsync(int customerId, int points, CancellationToken cancellationToken = default);

    // ── Catalog ─────────────────────────────────────────────────────────────
    Task<IReadOnlyList<PortalRouteDto>> GetRoutesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PortalVehicleDto>> GetVehiclesAsync(CancellationToken cancellationToken = default);

    Task<decimal?> GetRouteBasePriceAsync(int routeId, CancellationToken cancellationToken = default);

    Task<bool> VehicleExistsAndNotRetiredAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<decimal?> GetPerKmBasePriceAsync(CancellationToken cancellationToken = default);

    Task<int> GetFirstActiveRouteIdAsync(CancellationToken cancellationToken = default);

    Task<string?> GetRouteLabelAsync(int routeId, CancellationToken cancellationToken = default);

    Task<int?> GetVehicleSeatingCapacityAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<string?> GetVehicleLabelAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<string?> GetBookingNumberAsync(int bookingId, CancellationToken cancellationToken = default);

    Task<int?> GetPromoCodeIdAsync(string code, CancellationToken cancellationToken = default);

    Task<(int Id, decimal? Pct, decimal? Fixed)?> GetActivePromoAsync(string code, CancellationToken cancellationToken = default);

    // ── Bookings ────────────────────────────────────────────────────────────
    Task<IReadOnlyList<PortalBookingCardRow>> GetBookingCardsAsync(
        IReadOnlyList<int> customerIds, CancellationToken cancellationToken = default);

    Task<PortalBookingDetailHeadRow?> GetBookingDetailHeadAsync(
        int bookingId, IReadOnlyList<int> customerIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PortalPaymentLineDto>> GetBookingPaymentsAsync(
        int bookingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetBookingSeatsAsync(int bookingId, CancellationToken cancellationToken = default);

    Task ApplyPortalBookingExtrasAsync(PortalBookingExtrasUpdate update, CancellationToken cancellationToken = default);

    Task<bool> IsSeatTakenAsync(
        int vehicleId, string seatLabel, DateTime windowStart, DateTime windowEnd,
        CancellationToken cancellationToken = default);

    Task InsertBookingSeatAsync(int bookingId, string seatLabel, CancellationToken cancellationToken = default);

    Task<(int Status, int? VehicleId)?> GetBookingStatusVehicleAsync(int bookingId, CancellationToken cancellationToken = default);

    Task<(decimal? Speed, DateTime? UpdatedAt)?> GetVehicleLiveLocationAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<string?> GetStartedBookingDriverPhoneAsync(int bookingId, CancellationToken cancellationToken = default);

    Task<PortalInvoiceRow?> GetInvoiceRowAsync(int bookingId, CancellationToken cancellationToken = default);

    Task<(int Status, DateTime PickupTime)?> GetBookingStatusPickupAsync(int bookingId, CancellationToken cancellationToken = default);

    Task CancelBookingAsync(int bookingId, CancellationToken cancellationToken = default);

    Task<PortalCheckoutBookingRow?> GetCheckoutBookingAsync(int bookingId, CancellationToken cancellationToken = default);

    // ── Addresses / notifications / loyalty / wallet / favorites ────────────
    Task<int> SaveAddressAsync(
        int customerId, string label, string addressLine, double? lat, double? lng,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PortalSavedAddressDto>> GetSavedAddressesAsync(
        int customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PortalCustomerNotificationDto>> GetCustomerNotificationsAsync(
        int customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(string SeatLabel, int RowIndex, int ColIndex)>> GetVehicleSeatLayoutsAsync(
        int vehicleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetBookedSeatsNearPickupAsync(
        int vehicleId, DateTime pickupTime, CancellationToken cancellationToken = default);

    Task<(int Points, string Tier)> GetLoyaltyAsync(int customerId, CancellationToken cancellationToken = default);

    Task EnsureWalletRowAsync(int customerId, CancellationToken cancellationToken = default);

    Task<decimal> GetWalletBalanceAsync(int customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PortalFavoriteRouteDto>> GetFavoriteRoutesAsync(
        int customerId, CancellationToken cancellationToken = default);

    Task<int> AddFavoriteRouteAsync(
        int customerId, int routeId, string? label, CancellationToken cancellationToken = default);
}

public sealed class PortalBookingCardRow
{
    public int Id { get; init; }
    public string BookingNumber { get; init; } = "";
    public string RouteLabel { get; init; } = "";
    public DateTime PickupTime { get; init; }
    public int Status { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
}

public sealed class PortalBookingDetailHeadRow
{
    public int Id { get; init; }
    public string BookingNumber { get; init; } = "";
    public string RouteLabel { get; init; } = "";
    public DateTime PickupTime { get; init; }
    public int PassengerCount { get; init; }
    public string? VehicleName { get; init; }
    public int Status { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public string? PickupAddress { get; init; }
    public string? DropoffAddress { get; init; }
    public int? DriverId { get; init; }
    public string? DriverName { get; init; }
    public decimal? DriverRating { get; init; }
    public int? DriverYears { get; init; }
}

public sealed class PortalInvoiceRow
{
    public string BookingNumber { get; init; } = "";
    public string RouteLabel { get; init; } = "";
    public DateTime PickupTime { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public string CustomerName { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
}

public sealed class PortalCheckoutBookingRow
{
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public string? Email { get; init; }
}

public sealed class PortalBookingExtrasUpdate
{
    public int BookingId { get; init; }
    public string? PreferredPaymentMethod { get; init; }
    public string? PickupAddress { get; init; }
    public string? DropoffAddress { get; init; }
    public double? PickupLat { get; init; }
    public double? PickupLng { get; init; }
    public double? DropLat { get; init; }
    public double? DropLng { get; init; }
    public decimal? QuotedDistanceKm { get; init; }
    public int? QuotedDurationMinutes { get; init; }
    public int? AdultCount { get; init; }
    public int? ChildCount { get; init; }
    public int? LuggageCount { get; init; }
    public int? PromoCodeId { get; init; }
    public decimal DiscountAmount { get; init; }
}
