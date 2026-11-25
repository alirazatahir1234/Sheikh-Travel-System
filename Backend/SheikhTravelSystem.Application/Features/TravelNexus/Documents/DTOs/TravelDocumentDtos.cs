namespace SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;

public class TravelApplicantDocumentDto
{
    public int Id { get; set; }
    public int ApplicantId { get; set; }
    public int TravelerId { get; set; }
    public string TravelerName { get; set; } = string.Empty;
    public int DocumentTypeId { get; set; }
    public string DocumentTypeCode { get; set; } = string.Empty;
    public string DocumentTypeName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    /// <summary>True when requested for this applicant only (not from the checklist).</summary>
    public bool IsAdHoc { get; set; }
    public int? MinValidityDays { get; set; }
    public string? Instructions { get; set; }
    /// <summary>Missing | Uploaded | Verified | Rejected | Waived.</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public string? ReviewNote { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? CurrentFileId { get; set; }
    public IReadOnlyList<TravelDocumentFileDto> Files { get; set; } = [];
}

/// <summary>File metadata only — contents are streamed by the audited download endpoint.</summary>
public class TravelDocumentFileDto
{
    public int Id { get; set; }
    public int ApplicantDocumentId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class TravelCaseDocumentsDto
{
    public int VisaCaseId { get; set; }
    public string CaseStatus { get; set; } = string.Empty;
    public int? ChecklistTemplateId { get; set; }
    public string? ChecklistName { get; set; }
    public int? ChecklistVersion { get; set; }
    public int MandatoryTotal { get; set; }
    public int MandatoryComplete { get; set; }
    /// <summary>Documents can be uploaded/requested/reviewed only while the case is DocumentsPending.</summary>
    public bool CanEdit { get; set; }
    public IReadOnlyList<TravelApplicantDocumentDto> Requirements { get; set; } = [];
}

public class TravelMissingDocumentsItemDto
{
    public int VisaCaseId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string VisaTypeName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? IntendedTravelDate { get; set; }
    /// <summary>Mandatory requirements still Missing, Uploaded (unreviewed) or Rejected.</summary>
    public int MandatoryOutstanding { get; set; }
}

public record RequestTravelDocumentDto(int ApplicantId, int DocumentTypeId, bool IsMandatory, string? Instructions);

public record VerifyTravelDocumentDto(DateTime? ExpiryDate, string? Note);

public record ReviewReasonDto(string Reason);

/// <summary>Requirement to create for a traveler on a case (from a checklist item or ad hoc).</summary>
public sealed record TravelRequirementSeed(int TravelerId, int DocumentTypeId, int? ChecklistItemId, bool IsMandatory,
    int? MinValidityDays, string? Instructions);

/// <summary>Facts about one requirement used by document command handlers.</summary>
public sealed record TravelRequirementFacts(int Id, int VisaCaseId, int ApplicantId, int DocumentTypeId, string DocumentTypeName,
    bool IsMandatory, int? MinValidityDays, string Status, int? ChecklistItemId, int? CurrentFileId);

/// <summary>Facts about one stored file used by the download handler.</summary>
public sealed record TravelDocumentFileFacts(int Id, int VisaCaseId, string StorageKey, string OriginalFileName, string ContentType);

public sealed record TravelNewDocumentFile(string StorageKey, string OriginalFileName, string ContentType, long SizeBytes, string Sha256);

/// <summary>Streamed file returned by the download query.</summary>
public sealed record TravelDocumentDownload(Stream Content, string ContentType, string FileName);
