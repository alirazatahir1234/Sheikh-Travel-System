namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

public class TravelEnquiryListItemDto
{
    public int Id { get; set; }
    public string EnquiryNumber { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    /// <summary>Customer name when linked, otherwise the lead name.</summary>
    public string DisplayName { get; set; } = string.Empty;
    public string? DisplayPhone { get; set; }
    public string ServiceType { get; set; } = string.Empty;
    public string? DestinationCountryCode { get; set; }
    public string? DestinationCountryName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int? AssignedAgentUserId { get; set; }
    public string? AssignedAgentName { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TravelEnquiryDto : TravelEnquiryListItemDto
{
    public string? LeadName { get; set; }
    public string? LeadPhone { get; set; }
    public string? LeadEmail { get; set; }
    public int? VisaTypeId { get; set; }
    public string? VisaTypeName { get; set; }
    public DateTime? TravelDateFrom { get; set; }
    public DateTime? TravelDateTo { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public int Infants { get; set; }
    public decimal? Budget { get; set; }
    public string? CurrencyCode { get; set; }
    public int? LeadSourceId { get; set; }
    public string? LeadSourceName { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public int? LostReasonId { get; set; }
    public string? LostReasonName { get; set; }
    public string? LostNotes { get; set; }
    public int? ConvertedVisaCaseId { get; set; }
    public string? ConvertedVisaCaseNumber { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
    /// <summary>Actions the state machine allows now (Edit, LogActivity, MarkContacted, …, Convert).</summary>
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<TravelEnquiryActivityDto> Activities { get; set; } = [];
}

public class TravelEnquiryActivityDto
{
    public int Id { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
    public string? CreatedByName { get; set; }
}

/// <summary>Create/update payload. Either <c>CustomerId</c> or lead name + phone is required.</summary>
public record SaveTravelEnquiryDto(
    int? CustomerId,
    string? LeadName,
    string? LeadPhone,
    string? LeadEmail,
    string ServiceType,
    string? DestinationCountryCode,
    int? VisaTypeId,
    DateTime? TravelDateFrom,
    DateTime? TravelDateTo,
    int Adults,
    int Children,
    int Infants,
    decimal? Budget,
    string? CurrencyCode,
    int? LeadSourceId,
    int? AssignedAgentUserId,
    int? BranchId,
    string Priority,
    DateTime? NextFollowUpAt,
    string? Notes);

public record LogTravelEnquiryActivityDto(
    string ActivityType,
    string Direction,
    string Summary,
    string? Outcome,
    DateTime? OccurredAt,
    DateTime? NextFollowUpAt);

public record MarkTravelEnquiryLostDto(int LostReasonId, string? LostNotes);

/// <summary>
/// Conversion: use <c>ExistingCustomerId</c>, else the enquiry's linked customer, else create a customer from the
/// lead fields (<c>NewCustomerKind</c>). Optionally open a Draft visa case.
/// </summary>
public record ConvertTravelEnquiryDto(
    int? ExistingCustomerId,
    string? NewCustomerKind,
    bool CreateVisaCase,
    string? VisaCountryCode,
    int? VisaTypeId,
    DateTime? IntendedTravelDate);

public class TravelEnquirySummaryDto
{
    /// <summary>Current count per status (every status present, zero-filled).</summary>
    public IReadOnlyDictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
    /// <summary>Sum of <see cref="Counts"/>.</summary>
    public int Total { get; set; }
    /// <summary>Open enquiries whose next follow-up is due now or overdue.</summary>
    public int FollowUpsDue { get; set; }
}

public sealed record TravelEnquiryListFilter(
    string? Search,
    string? Status,
    string? ServiceType,
    int? AgentUserId,
    int? BranchId,
    int? CustomerId,
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    bool FollowUpDue,
    DateTime NowUtc);

/// <summary>Status change applied atomically with a StatusChange activity.</summary>
public sealed record TravelEnquiryStatusChange(
    int EnquiryId,
    string ExpectedStatus,
    string NewStatus,
    string ActivitySummary,
    int? LostReasonId = null,
    string? LostNotes = null);

public sealed record TravelEnquiryNewCustomer(string Kind, string FullName, string Phone, string? Email, int? BranchId, int? AgentUserId, int? LeadSourceId);

public sealed record TravelEnquiryNewVisaCase(string CountryCode, int VisaTypeId, DateTime? IntendedTravelDate, int? BranchId, int? AgentUserId);

public sealed record TravelEnquiryConversionPlan(
    int EnquiryId,
    string ExpectedStatus,
    int? CustomerId,
    TravelEnquiryNewCustomer? NewCustomer,
    TravelEnquiryNewVisaCase? VisaCase);

public sealed record TravelEnquiryConversionResult(int CustomerId, int? VisaCaseId, string? VisaCaseNumber);
