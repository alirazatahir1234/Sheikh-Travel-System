namespace SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;

public class TravelChecklistTemplateListItemDto
{
    public int Id { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    /// <summary>Null = applies to every visa type of the country.</summary>
    public int? VisaTypeId { get; set; }
    public string? VisaTypeName { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    /// <summary>Draft | Active | Retired. Only drafts can be edited.</summary>
    public string Status { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TravelChecklistTemplateDto : TravelChecklistTemplateListItemDto
{
    public string? Notes { get; set; }
    public IReadOnlyList<TravelChecklistItemDto> Items { get; set; } = [];
}

public class TravelChecklistItemDto
{
    public int Id { get; set; }
    public int DocumentTypeId { get; set; }
    public string DocumentTypeCode { get; set; } = string.Empty;
    public string DocumentTypeName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    /// <summary>All | Adult | Minor | Male | Female | FemaleWithMahram.</summary>
    public string ApplicantCondition { get; set; } = "All";
    public int? MinValidityDays { get; set; }
    public string? Instructions { get; set; }
    public int SortOrder { get; set; }
}

public record SaveTravelChecklistTemplateDto(string CountryCode, int? VisaTypeId, string Name, string? Notes);

public record SaveTravelChecklistItemDto(int DocumentTypeId, bool IsMandatory, string ApplicantCondition, int? MinValidityDays, string? Instructions);

/// <summary>The checklist a case was (or would be) built from.</summary>
public sealed record TravelChecklistSnapshotSource(int TemplateId, int Version, IReadOnlyList<TravelChecklistItemDto> Items);
