using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

public class TravelVisaCaseListItemDto
{
    public int Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public int VisaTypeId { get; set; }
    public string VisaTypeName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int ApplicantCount { get; set; }
    public int ApprovedCount { get; set; }
    public DateTime? IntendedTravelDate { get; set; }
    public DateTime? AppointmentAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? AssignedAgentUserId { get; set; }
    public string? AssignedAgentName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TravelVisaCaseDto : TravelVisaCaseListItemDto
{
    public int? EnquiryId { get; set; }
    public string? EnquiryNumber { get; set; }
    public string? SubmissionReference { get; set; }
    public DateTime? ExpectedDecisionDate { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? OnHoldFromStatus { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public decimal? QuotedServiceFee { get; set; }
    public decimal? QuotedEmbassyFee { get; set; }
    public string? CurrencyCode { get; set; }
    public string? CancelReason { get; set; }
    public string? Notes { get; set; }
    public int? ChecklistTemplateId { get; set; }
    public int? ChecklistVersion { get; set; }
    /// <summary>Mandatory document requirements of active applicants, and how many are Verified or Waived.</summary>
    public int DocumentsMandatoryTotal { get; set; }
    public int DocumentsMandatoryComplete { get; set; }
    /// <summary>Actions the state machine allows now (Edit, StartDocuments, …, RecordDecision).</summary>
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<TravelVisaApplicantDto> Applicants { get; set; } = [];
    public IReadOnlyList<TravelVisaHistoryDto> History { get; set; } = [];
}

public class TravelVisaApplicantDto
{
    public int Id { get; set; }
    public int TravelerId { get; set; }
    public string TravelerName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    /// <summary>Masked unless the caller holds Travel.Traveler.ViewPII.</summary>
    public string? PassportNumber { get; set; }
    public DateTime? PassportExpiryDate { get; set; }
    /// <summary>True when the passport is missing or expires less than six months after the intended travel date.</summary>
    public bool PassportWarning { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? VisaNumber { get; set; }
    public DateTime? VisaIssueDate { get; set; }
    public DateTime? VisaValidFrom { get; set; }
    public DateTime? VisaExpiryDate { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Notes { get; set; }
}

public class TravelVisaHistoryDto
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? ApplicantId { get; set; }
    public string? ApplicantName { get; set; }
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? Note { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
}

public record CreateTravelVisaCaseDto(
    int CustomerId,
    int VisaTypeId,
    int? EnquiryId,
    DateTime? IntendedTravelDate,
    DateTime? AppointmentAt,
    DateTime? ExpectedDecisionDate,
    string Priority,
    int? AssignedAgentUserId,
    decimal? QuotedServiceFee,
    decimal? QuotedEmbassyFee,
    string? CurrencyCode,
    string? Notes);

public record UpdateTravelVisaCaseDto(
    DateTime? IntendedTravelDate,
    DateTime? AppointmentAt,
    DateTime? ExpectedDecisionDate,
    string Priority,
    int? AssignedAgentUserId,
    decimal? QuotedServiceFee,
    decimal? QuotedEmbassyFee,
    string? CurrencyCode,
    string? Notes);

public record TransitionTravelVisaCaseDto(string Action, string? Note);

public record SubmitTravelVisaCaseDto(DateTime SubmittedAt, string? SubmissionReference, DateTime? ExpectedDecisionDate, string? Note);

public record AddTravelVisaApplicantsDto(IReadOnlyList<int> TravelerIds);

public record RecordTravelVisaDecisionDto(
    string Decision,
    string? VisaNumber,
    DateTime? VisaIssueDate,
    DateTime? VisaValidFrom,
    DateTime? VisaExpiryDate,
    string? RejectionReason,
    string? Notes);

public class TravelVisaCaseSummaryDto
{
    /// <summary>Current count per status (every status present, zero-filled).</summary>
    public IReadOnlyDictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
    /// <summary>Sum of <see cref="Counts"/>.</summary>
    public int Total { get; set; }
}

public sealed record TravelVisaCaseListFilter(
    string? Search,
    string? Status,
    string? CountryCode,
    int? VisaTypeId,
    int? AgentUserId,
    int? CustomerId,
    DateTime? CreatedFrom,
    DateTime? CreatedTo);

/// <summary>Minimal case facts used by command handlers for state-machine and checklist checks.</summary>
public sealed record TravelVisaCaseState(int Id, int CustomerId, string Status, string? OnHoldFromStatus, int? BranchId,
    string CountryCode, int VisaTypeId, DateTime? IntendedTravelDate, int? ChecklistTemplateId);

/// <summary>Atomic status change + history row. Optional fields are written only when supplied.</summary>
public sealed record TravelVisaCaseStatusChange(
    int CaseId,
    string ExpectedStatus,
    string NewStatus,
    string? NewOnHoldFromStatus,
    string? Note,
    DateTime? SubmittedAt = null,
    string? SubmissionReference = null,
    DateTime? ExpectedDecisionDate = null,
    string? CancelReason = null,
    IReadOnlyList<TravelRequirementSeed>? RequirementSeeds = null,
    int? ChecklistTemplateId = null,
    int? ChecklistVersion = null);

public sealed record TravelVisaDecisionResult(bool Applied, string? NewCaseStatus);
