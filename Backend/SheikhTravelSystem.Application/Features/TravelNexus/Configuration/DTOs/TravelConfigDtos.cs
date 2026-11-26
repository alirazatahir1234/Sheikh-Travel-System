namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

public record TravelCountryDto(string Code, string Name);

public class TravelLookupItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Tenant branches and staff users (id + name only) for TravelNexus dropdowns.</summary>
public class TravelLookupsDto
{
    public IReadOnlyList<TravelLookupItemDto> Branches { get; set; } = [];
    public IReadOnlyList<TravelLookupItemDto> Agents { get; set; } = [];
}

public class TravelVisaTypeDto
{
    public int Id { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string EntryType { get; set; } = string.Empty;
    public int? ValidityDays { get; set; }
    public int? StayDays { get; set; }
    public int? ProcessingDays { get; set; }
    public decimal? DefaultFee { get; set; }
    public string? CurrencyCode { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public record SaveTravelVisaTypeDto(
    string CountryCode,
    string Name,
    string Category,
    string EntryType,
    int? ValidityDays,
    int? StayDays,
    int? ProcessingDays,
    decimal? DefaultFee,
    string? CurrencyCode,
    bool IsActive,
    string? Notes);

public class TravelDocumentTypeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RequiresExpiryDate { get; set; }
    public bool IsActive { get; set; }
}

public record SaveTravelDocumentTypeDto(
    string Code,
    string Name,
    string? Description,
    bool RequiresExpiryDate,
    bool IsActive);

public class TravelLeadSourceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public record SaveTravelLeadSourceDto(string Name, bool IsActive);

public class TravelLostReasonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public record SaveTravelLostReasonDto(string Name, bool IsActive);

public class TravelSettingsDto
{
    public string? DefaultCurrencyCode { get; set; }
    public string EnquiryPrefix { get; set; } = "ENQ";
    public string VisaCasePrefix { get; set; } = "VC";
    public string InvoicePrefix { get; set; } = "TINV";
    public string GroupPrefix { get; set; } = "GRP";
    public string BookingPrefix { get; set; } = "BKG";
    public string ReceiptPrefix { get; set; } = "TRCP";
    public string CreditNotePrefix { get; set; } = "TCN";
    public string RefundPrefix { get; set; } = "TRF";
    /// <summary>Days between issue and due date suggested for new invoices.</summary>
    public int DefaultPaymentTermsDays { get; set; } = 7;
    public int PassportExpiryWarningDays { get; set; } = 180;
}

public record UpdateTravelSettingsDto(
    string? DefaultCurrencyCode,
    string EnquiryPrefix,
    string VisaCasePrefix,
    string InvoicePrefix,
    int PassportExpiryWarningDays,
    string? GroupPrefix = null,
    string? BookingPrefix = null,
    string? ReceiptPrefix = null,
    string? CreditNotePrefix = null,
    string? RefundPrefix = null,
    int? DefaultPaymentTermsDays = null);
