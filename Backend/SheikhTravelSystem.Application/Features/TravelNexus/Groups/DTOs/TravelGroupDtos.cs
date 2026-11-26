namespace SheikhTravelSystem.Application.Features.TravelNexus.Groups.DTOs;

public class TravelGroupListItemDto
{
    public int Id { get; set; }
    public string GroupNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string PackageType { get; set; } = string.Empty;
    public DateTime DepartureDate { get; set; }
    public DateTime ReturnDate { get; set; }
    public int Capacity { get; set; }
    /// <summary>Registered + confirmed (not cancelled).</summary>
    public int ActiveMembers { get; set; }
    public int ConfirmedMembers { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LeaderDisplayName { get; set; }
    public int? VisaCaseId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TravelGroupDto : TravelGroupListItemDto
{
    public int? GroupLeaderTravelerId { get; set; }
    public string? LeaderName { get; set; }
    public string? LeaderPhone { get; set; }
    public string? VisaCaseNumber { get; set; }
    public string? VisaCaseStatus { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
    public string PackageCurrencyCode { get; set; } = string.Empty;
    public string PackageStatus { get; set; } = string.Empty;
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public TravelGroupReadinessDto Readiness { get; set; } = new();
    public IReadOnlyList<TravelGroupMemberDto> Members { get; set; } = [];
    public IReadOnlyList<TravelGroupStayDto> Stays { get; set; } = [];
    public IReadOnlyList<TravelGroupRoomDto> Rooms { get; set; } = [];
    public IReadOnlyList<TravelGroupTransportDto> Transport { get; set; } = [];
    public IReadOnlyList<TravelGroupHistoryDto> History { get; set; } = [];
}

public class TravelGroupMemberDto
{
    public int Id { get; set; }
    public int TravelerId { get; set; }
    public string TravelerName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int? MahramTravelerId { get; set; }
    /// <summary>Masked unless the caller holds Travel.Traveler.ViewPII.</summary>
    public string? PassportNumber { get; set; }
    public DateTime? PassportExpiryDate { get; set; }
    public string RoomPreference { get; set; } = string.Empty;
    public string PriceCategory { get; set; } = string.Empty;
    public decimal? QuotedPrice { get; set; }
    public string? CurrencyCode { get; set; }
    /// <summary>Registered | Confirmed | Cancelled.</summary>
    public string Status { get; set; } = string.Empty;
    public string? CancelReason { get; set; }
    /// <summary>Decision on the group visa case (null before visa processing).</summary>
    public string? VisaDecision { get; set; }
    public string? VisaNumber { get; set; }
    public DateTime? VisaExpiryDate { get; set; }
}

public class TravelGroupStayDto
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Sequence { get; set; }
    public string? ConfirmationNumber { get; set; }
    public int RoomCount { get; set; }
    public int AssignedCount { get; set; }
}

public class TravelGroupRoomDto
{
    public int Id { get; set; }
    public int GroupHotelId { get; set; }
    public string? RoomNumber { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public int Capacity { get; set; }
    /// <summary>Male | Female | Family.</summary>
    public string GenderPolicy { get; set; } = string.Empty;
    public IReadOnlyList<int> MemberIds { get; set; } = [];
}

public class TravelGroupTransportDto
{
    public int Id { get; set; }
    public string LegType { get; set; } = string.Empty;
    public DateTime DepartAt { get; set; }
    public string FromPlace { get; set; } = string.Empty;
    public string ToPlace { get; set; } = string.Empty;
    public string? ProviderName { get; set; }
    public string? VehicleType { get; set; }
    public int? Seats { get; set; }
    public int? FleetBookingId { get; set; }
    public string? FleetBookingNumber { get; set; }
    public string? Notes { get; set; }
}

public class TravelGroupHistoryDto
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? MemberId { get; set; }
    public string? MemberName { get; set; }
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? Note { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
}

/// <summary>What still blocks "Mark ready" (visa approvals and rooming).</summary>
public class TravelGroupReadinessDto
{
    public int ConfirmedMembers { get; set; }
    public int UnconfirmedMembers { get; set; }
    public int MembersWithApprovedVisa { get; set; }
    public int MembersWithoutVisa => ConfirmedMembers - MembersWithApprovedVisa;
    /// <summary>Confirmed members missing a room in at least one stay.</summary>
    public int MembersWithoutRoom { get; set; }
    /// <summary>Confirmed pilgrims without an issued ticket on an outbound and a return flight (or a round trip).</summary>
    public int MembersWithoutTickets { get; set; }
    public int Stays { get; set; }
    public bool VisaCaseDecided { get; set; }
    public bool IsReady => ConfirmedMembers > 0 && UnconfirmedMembers == 0 && VisaCaseDecided
                           && MembersWithoutVisa == 0 && MembersWithoutRoom == 0 && MembersWithoutTickets == 0 && Stays > 0;
}

public class TravelGroupSummaryDto
{
    public IReadOnlyDictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
    public int Total { get; set; }
}

// ── Manifest ────────────────────────────────────────────────────────────

public class TravelGroupManifestDto
{
    public string GroupNumber { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public DateTime DepartureDate { get; set; }
    public DateTime ReturnDate { get; set; }
    public string? LeaderName { get; set; }
    public string? LeaderPhone { get; set; }
    public string? VisaCaseNumber { get; set; }
    public bool PiiMasked { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public IReadOnlyList<TravelGroupStayDto> Stays { get; set; } = [];
    public IReadOnlyList<TravelGroupTransportDto> Transport { get; set; } = [];
    public IReadOnlyList<TravelManifestPilgrimDto> Pilgrims { get; set; } = [];
}

public class TravelManifestPilgrimDto
{
    public int No { get; set; }
    public int MemberId { get; set; }
    public string Surname { get; set; } = string.Empty;
    public string GivenNames { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? NationalityCode { get; set; }
    public string? PassportNumber { get; set; }
    public DateTime? PassportExpiryDate { get; set; }
    public string? VisaNumber { get; set; }
    public DateTime? VisaExpiryDate { get; set; }
    public string? MahramName { get; set; }
    public string? Phone { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    /// <summary>One entry per stay in stay order, e.g. "Makkah · Hilton · Room 512 (Quad)"; empty when unassigned.</summary>
    public IReadOnlyList<string> Rooms { get; set; } = [];
}

// ── Inputs ──────────────────────────────────────────────────────────────

public record CreateTravelGroupDto(int PackageId, string Name, DateTime DepartureDate, int Capacity, int? BranchId, string? Notes);

public record UpdateTravelGroupDto(string Name, DateTime DepartureDate, DateTime ReturnDate, int Capacity,
    int? GroupLeaderTravelerId, string? LeaderName, string? LeaderPhone, string? Notes);

public record TransitionTravelGroupDto(string Action, string? Note);

public record RegisterTravelGroupMemberItem(int TravelerId, string RoomPreference, string PriceCategory);
public record RegisterTravelGroupMembersDto(int CustomerId, IReadOnlyList<RegisterTravelGroupMemberItem> Members);

public record UpdateTravelGroupMemberDto(string RoomPreference, string PriceCategory, decimal? QuotedPrice);
public record CancelTravelGroupMemberDto(string Reason);

public record SaveTravelGroupStayDto(int HotelId, DateTime CheckIn, DateTime CheckOut, string? ConfirmationNumber);

public record TravelRoomingRoomDto(string? RoomNumber, string RoomType, string GenderPolicy, IReadOnlyList<int> MemberIds);
public record SaveTravelStayRoomingDto(IReadOnlyList<TravelRoomingRoomDto> Rooms);

public record SaveTravelTransportLegDto(string LegType, DateTime DepartAt, string FromPlace, string ToPlace, string? ProviderName,
    string? VehicleType, int? Seats, int? FleetBookingId, string? Notes);

// ── Internal facts ──────────────────────────────────────────────────────

public sealed record TravelGroupState(int Id, string Status, int PackageId, int Capacity, DateTime DepartureDate, DateTime ReturnDate,
    int? VisaCaseId, int? BranchId, string Name);

/// <summary>Pilgrim facts for room allocation and validation.</summary>
public sealed record TravelGroupMemberFacts(int MemberId, int TravelerId, int CustomerId, string Gender, string PriceCategory,
    string RoomPreference, int? MahramTravelerId, string Status);

public sealed record TravelGroupStatusChange(int GroupId, string ExpectedStatus, string NewStatus, string? Note, string? CancelReason = null);

/// <summary>
/// How a cancelled pilgrim leaves the group visa case, applied in the same transaction as the cancellation:
/// removed while the case is pre-submission (<see cref="Remove"/>), otherwise recorded as a Withdrawn decision.
/// <see cref="ExpectedCaseStatus"/> is the status the case was read in; a different status rolls the whole cancellation back.
/// </summary>
public sealed record TravelVisaWithdrawal(int CaseId, int ApplicantId, string ExpectedCaseStatus, bool Remove, string Note);

public sealed record TravelGroupStayPlan(int HotelId, DateTime CheckIn, DateTime CheckOut, int Sequence);

public sealed record TravelGroupNewMember(int TravelerId, int CustomerId, string RoomPreference, string PriceCategory, decimal? QuotedPrice, string? CurrencyCode);

public sealed record TravelGroupListFilter(string? Search, string? Status, int? PackageId, DateTime? DepartureFrom, DateTime? DepartureTo);
