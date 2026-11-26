using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Booking files and their flight / hotel services, cancellations and history. Every statement filters on TenantId;
/// files are clamped by branch scope. Status-dependent writes check the expected status and return false on conflict.
/// </summary>
public interface ITravelBookingRepository
{
    // ── Files ──
    Task<TravelBookingState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<PagedResult<TravelBookingListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelBookingListFilter filter,
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<int> CountTicketingDueAsync(int tenantId, TravelBranchScope scope, DateTime untilUtc, CancellationToken cancellationToken = default);
    /// <summary>Header, services, cancellations and history (passport numbers raw — masked by the caller).</summary>
    Task<TravelBookingDto?> GetDetailAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<TravelBookingServiceFacts> GetServiceFactsAsync(int bookingId, int tenantId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(int tenantId, CreateTravelBookingDto dto, int? userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, int tenantId, UpdateTravelBookingDto dto, CancellationToken cancellationToken = default);
    Task<bool> ChangeStatusAsync(int tenantId, int id, string expectedStatus, string newStatus, string? note, string? cancelReason,
        int? userId, DateTime utcNow, CancellationToken cancellationToken = default);

    // ── Flights ──
    Task<TravelFlightState?> GetFlightStateAsync(int bookingId, int flightId, int tenantId, CancellationToken cancellationToken = default);
    Task<bool> PnrInUseAsync(int tenantId, string pnr, int? excludeFlightId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> TicketNumbersInUseAsync(int tenantId, IReadOnlyList<string> ticketNumbers, int flightId, CancellationToken cancellationToken = default);
    /// <summary>Creates (flightId null) or updates a flight; segments are replaced, passengers merged by traveler when <paramref name="replacePassengers"/>.</summary>
    Task<int> SaveFlightAsync(int tenantId, int bookingId, int? flightId, SaveTravelFlightDto dto, IReadOnlyList<TravelFlightSegmentWrite> segments,
        bool replacePassengers, int? userId, CancellationToken cancellationToken = default);
    Task<bool> TransitionFlightAsync(int tenantId, int bookingId, int flightId, string expectedStatus, string newStatus, string? pnr,
        DateTime? ticketingTimeLimitUtc, string? note, int? userId, CancellationToken cancellationToken = default);
    Task<bool> SetTicketsAsync(int tenantId, int bookingId, int flightId, string expectedStatus, IReadOnlyList<TravelTicketNumberDto> tickets,
        int? userId, CancellationToken cancellationToken = default);
    /// <summary>Cancels the flight; issued tickets take <paramref name="ticketOutcome"/> (Voided | Refunded).</summary>
    Task<int?> CancelFlightAsync(int tenantId, int bookingId, int flightId, string expectedStatus, string ticketOutcome,
        TravelServiceCancellationWrite cancellation, int? userId, CancellationToken cancellationToken = default);
    Task<int?> CancelPassengerAsync(int tenantId, int bookingId, int flightId, int passengerId, string ticketOutcome,
        TravelServiceCancellationWrite cancellation, int? userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteFlightAsync(int tenantId, int bookingId, int flightId, int? userId, CancellationToken cancellationToken = default);

    // ── Hotels ──
    Task<TravelHotelBookingState?> GetHotelStateAsync(int bookingId, int hotelBookingId, int tenantId, CancellationToken cancellationToken = default);
    Task<int> SaveHotelAsync(int tenantId, int bookingId, int? hotelBookingId, SaveTravelHotelBookingDto dto, int? userId, CancellationToken cancellationToken = default);
    /// <summary>On Confirm with a group stay link, the confirmation number is copied to the stay.</summary>
    Task<bool> TransitionHotelAsync(int tenantId, int bookingId, int hotelBookingId, string expectedStatus, string newStatus,
        string? confirmationNumber, string? note, int? userId, CancellationToken cancellationToken = default);
    Task<int?> CancelHotelAsync(int tenantId, int bookingId, int hotelBookingId, string expectedStatus,
        TravelServiceCancellationWrite cancellation, int? userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteHotelAsync(int tenantId, int bookingId, int hotelBookingId, int? userId, CancellationToken cancellationToken = default);

    // ── Cancellations ──
    Task<TravelCancellationState?> GetCancellationAsync(int bookingId, int cancellationId, int tenantId, CancellationToken cancellationToken = default);
    Task<bool> UpdateRefundStatusAsync(int tenantId, int bookingId, int cancellationId, string expectedStatus, string newStatus, string? note,
        int? userId, CancellationToken cancellationToken = default);

    // ── Reference checks (same tenant) ──
    Task<bool> SupplierExistsAsync(int tenantId, int supplierId, CancellationToken cancellationToken = default);
    Task<bool> HotelExistsAsync(int tenantId, int hotelId, CancellationToken cancellationToken = default);
    /// <summary>The stay belongs to the given group of this tenant.</summary>
    Task<bool> GroupStayExistsAsync(int tenantId, int groupId, int groupHotelId, CancellationToken cancellationToken = default);
    /// <summary>Travelers that may travel on the file: the customer's travelers, or the group's active pilgrims.</summary>
    Task<IReadOnlyList<(int TravelerId, int? GroupMemberId)>> GetEligibleTravelersAsync(int tenantId, int? customerId, int? groupId,
        CancellationToken cancellationToken = default);
    Task<bool> EnquiryBelongsAsync(int tenantId, int enquiryId, int? customerId, CancellationToken cancellationToken = default);
    Task<bool> VisaCaseBelongsAsync(int tenantId, int visaCaseId, int? customerId, int? groupId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelBookingListItemDto>> GetByCustomerAsync(int customerId, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelBookingListItemDto>> GetByGroupAsync(int groupId, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
}
