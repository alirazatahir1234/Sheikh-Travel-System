namespace SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;

// ── Read models ─────────────────────────────────────────────────────────

public class TravelBookingListItemDto
{
    public int Id { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int? GroupId { get; set; }
    /// <summary>Customer name, or the group name for a group booking.</summary>
    public string OwnerName { get; set; } = string.Empty;
    /// <summary>Open | Completed | Cancelled.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Earliest flight departure (UTC date) or hotel check-in among non-cancelled services.</summary>
    public DateTime? FirstTravelDate { get; set; }
    public DateTime? LastTravelDate { get; set; }
    public int FlightCount { get; set; }
    public int HotelCount { get; set; }
    public string? Pnrs { get; set; }
    /// <summary>Earliest ticketing time limit among flights on hold.</summary>
    public DateTime? NextTicketingLimitUtc { get; set; }
    public string? AssignedAgentName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TravelBookingDto : TravelBookingListItemDto
{
    public string? CustomerName { get; set; }
    public string? GroupName { get; set; }
    public string? GroupNumber { get; set; }
    public int? EnquiryId { get; set; }
    public string? EnquiryNumber { get; set; }
    public int? VisaCaseId { get; set; }
    public string? VisaCaseNumber { get; set; }
    public int? AssignedAgentUserId { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CancelReason { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<TravelFlightBookingDto> Flights { get; set; } = [];
    public IReadOnlyList<TravelHotelBookingDto> Hotels { get; set; } = [];
    public IReadOnlyList<TravelBookingCancellationDto> Cancellations { get; set; } = [];
    public IReadOnlyList<TravelBookingHistoryDto> History { get; set; } = [];
    /// <summary>Sell and cost of non-cancelled services, per currency (no FX conversion).</summary>
    public IReadOnlyList<TravelBookingTotalDto> Totals { get; set; } = [];
}

public class TravelFlightBookingDto
{
    public int Id { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    /// <summary>Outbound | Return | RoundTrip | OneWay | Internal.</summary>
    public string Direction { get; set; } = string.Empty;
    public string? Pnr { get; set; }
    public string? AirlinePnr { get; set; }
    /// <summary>Requested | OnHold | Confirmed | Ticketed | Cancelled.</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime? TicketingTimeLimitUtc { get; set; }
    public decimal? SellAmount { get; set; }
    public decimal? CostAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<TravelFlightSegmentDto> Segments { get; set; } = [];
    public IReadOnlyList<TravelFlightPassengerDto> Passengers { get; set; } = [];
}

public class TravelFlightSegmentDto
{
    public int Id { get; set; }
    public int FlightBookingId { get; set; }
    public int Sequence { get; set; }
    public string AirlineCode { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public string FromAirport { get; set; } = string.Empty;
    public string FromCity { get; set; } = string.Empty;
    public string ToAirport { get; set; } = string.Empty;
    public string ToCity { get; set; } = string.Empty;
    /// <summary>Airport-local time as printed on the ticket (no offset).</summary>
    public DateTime DepartLocal { get; set; }
    public DateTime ArriveLocal { get; set; }
    public DateTime DepartUtc { get; set; }
    public DateTime ArriveUtc { get; set; }
    public string CabinClass { get; set; } = string.Empty;
    public string? BookingClass { get; set; }
    public string? Baggage { get; set; }
}

public class TravelFlightPassengerDto
{
    public int Id { get; set; }
    public int FlightBookingId { get; set; }
    public int TravelerId { get; set; }
    public string TravelerName { get; set; } = string.Empty;
    public int? GroupMemberId { get; set; }
    public string PassengerType { get; set; } = string.Empty;
    /// <summary>Masked unless the user holds Travel.Traveler.ViewPII.</summary>
    public string? PassportNumber { get; set; }
    public string? TicketNumber { get; set; }
    /// <summary>NotIssued | Issued | Voided | Refunded.</summary>
    public string TicketStatus { get; set; } = string.Empty;
}

public class TravelHotelBookingDto
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? HotelAddress { get; set; }
    public string? HotelPhone { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int? GroupHotelId { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Nights => (int)(CheckOut.Date - CheckIn.Date).TotalDays;
    public string MealPlan { get; set; } = string.Empty;
    public string? ConfirmationNumber { get; set; }
    /// <summary>Requested | Confirmed | CheckedIn | CheckedOut | NoShow | Cancelled.</summary>
    public string Status { get; set; } = string.Empty;
    public string? SpecialRequests { get; set; }
    public decimal? SellAmount { get; set; }
    public decimal? CostAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<TravelHotelBookingRoomDto> Rooms { get; set; } = [];
    public IReadOnlyList<TravelHotelBookingGuestDto> Guests { get; set; } = [];
}

public class TravelHotelBookingRoomDto
{
    public int HotelBookingId { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class TravelHotelBookingGuestDto
{
    public int HotelBookingId { get; set; }
    public int TravelerId { get; set; }
    public string TravelerName { get; set; } = string.Empty;
}

public class TravelBookingCancellationDto
{
    public int Id { get; set; }
    /// <summary>Flight | Hotel.</summary>
    public string ServiceType { get; set; } = string.Empty;
    public int? FlightBookingId { get; set; }
    public int? HotelBookingId { get; set; }
    public int? FlightPassengerId { get; set; }
    /// <summary>"PNR ABC123", "PNR ABC123 · Ali Khan" or "Hilton Makkah".</summary>
    public string ServiceLabel { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal SupplierPenalty { get; set; }
    public decimal CustomerPenalty { get; set; }
    public decimal RefundFromSupplier { get; set; }
    public decimal RefundToCustomer { get; set; }
    public string? CurrencyCode { get; set; }
    /// <summary>NotApplicable | Pending | ReceivedFromSupplier | PaidToCustomer | Closed.</summary>
    public string RefundStatus { get; set; } = string.Empty;
    public IReadOnlyList<string> NextRefundStatuses { get; set; } = [];
    public DateTime CancelledAt { get; set; }
    public string? CancelledByName { get; set; }
}

public class TravelBookingHistoryDto
{
    public int Id { get; set; }
    public string? ServiceType { get; set; }
    public int? ServiceId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? Note { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class TravelBookingTotalDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Sell { get; set; }
    public decimal Cost { get; set; }
    public decimal Margin => Sell - Cost;
}

public class TravelBookingSummaryDto
{
    public IReadOnlyDictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
    public int Total { get; set; }
    /// <summary>Flights on hold whose ticketing time limit falls within the next 72 hours (or has passed).</summary>
    public int TicketingDueNext72Hours { get; set; }
}

// ── Inputs ──────────────────────────────────────────────────────────────

public record CreateTravelBookingDto(int? CustomerId, int? GroupId, string Title, int? EnquiryId, int? VisaCaseId,
    int? AssignedAgentUserId, int? BranchId, string? Notes);
public record UpdateTravelBookingDto(string Title, int? EnquiryId, int? VisaCaseId, int? AssignedAgentUserId, string? Notes);
public record TransitionTravelBookingDto(string Action, string? Note);

public record SaveTravelFlightSegmentDto(string AirlineCode, string FlightNumber, string FromAirport, string ToAirport,
    DateTime DepartLocal, DateTime ArriveLocal, string CabinClass, string? BookingClass, string? Baggage);
public record SaveTravelFlightPassengerDto(int TravelerId, int? GroupMemberId, string PassengerType);
public record SaveTravelFlightDto(int? SupplierId, string Direction, string? Pnr, string? AirlinePnr, decimal? SellAmount,
    decimal? CostAmount, string? CurrencyCode, string? Notes, IReadOnlyList<SaveTravelFlightSegmentDto> Segments,
    IReadOnlyList<SaveTravelFlightPassengerDto> Passengers);
/// <summary>Hold needs PNR + time limit; Confirm needs a PNR.</summary>
public record TransitionTravelFlightDto(string Action, string? Pnr, DateTime? TicketingTimeLimitUtc, string? Note);
public record TravelTicketNumberDto(int PassengerId, string TicketNumber);
public record SetTravelTicketsDto(IReadOnlyList<TravelTicketNumberDto> Tickets);

public record SaveTravelHotelRoomDto(string RoomType, int Quantity);
public record SaveTravelHotelBookingDto(int HotelId, int? SupplierId, int? GroupHotelId, DateTime CheckIn, DateTime CheckOut,
    string MealPlan, string? ConfirmationNumber, string? SpecialRequests, decimal? SellAmount, decimal? CostAmount, string? CurrencyCode,
    IReadOnlyList<SaveTravelHotelRoomDto> Rooms, IReadOnlyList<int> GuestTravelerIds);
public record TransitionTravelHotelDto(string Action, string? ConfirmationNumber, string? Note);

/// <summary>Penalties and refunds in the service currency. For a flight passenger, <c>TicketOutcome</c> is Voided or Refunded.</summary>
public record CancelTravelServiceDto(string Reason, decimal SupplierPenalty, decimal CustomerPenalty, decimal RefundFromSupplier,
    decimal RefundToCustomer, string? TicketOutcome = null);
public record UpdateTravelRefundStatusDto(string RefundStatus, string? Note);

// ── Internal facts ──────────────────────────────────────────────────────

public sealed record TravelBookingState(int Id, string BookingNumber, string Status, int? CustomerId, int? GroupId, int? BranchId);

public sealed record TravelBookingServiceFacts(int OpenFlights, int OpenHotels, int UnfinishedServices, int CancelledServices,
    DateTime? LastServiceUtc);

public sealed record TravelFlightPassengerFacts(int Id, int TravelerId, int? GroupMemberId, string TicketStatus, string? TicketNumber);

public sealed record TravelFlightState(int Id, int BookingId, string Status, string? Pnr, decimal? SellAmount, decimal? CostAmount,
    string? CurrencyCode, int SegmentCount)
{
    public IReadOnlyList<TravelFlightPassengerFacts> Passengers { get; init; } = [];
}

public sealed record TravelHotelBookingState(int Id, int BookingId, string Status, DateTime CheckIn, DateTime CheckOut, int? GroupHotelId,
    decimal? SellAmount, decimal? CostAmount, string? CurrencyCode);

public sealed record TravelCancellationState(int Id, int BookingId, string RefundStatus);

/// <summary>Segment with UTC times derived from the airport zones.</summary>
public sealed record TravelFlightSegmentWrite(int Sequence, SaveTravelFlightSegmentDto Segment, DateTime DepartUtc, DateTime ArriveUtc);

public sealed record TravelServiceCancellationWrite(string Reason, decimal SupplierPenalty, decimal CustomerPenalty, decimal RefundFromSupplier,
    decimal RefundToCustomer, string? CurrencyCode, string RefundStatus);

/// <summary>An issued ticket on a ticketed flight of a group-owned booking (input of the group's ticket gate).</summary>
public sealed record TravelGroupTicketFact(int TravelerId, string Direction);

public sealed record TravelBookingListFilter(string? Search, string? Status, string? OwnerType, int? CustomerId, int? GroupId,
    DateTime? TravelFrom, DateTime? TravelTo)
{
    /// <summary>Only files with a flight on hold whose ticketing limit is before this instant.</summary>
    public DateTime? TicketingDueBeforeUtc { get; init; }
}
