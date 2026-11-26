namespace SheikhTravelSystem.Application.Features.TravelNexus.Profitability.DTOs;

/// <summary>Profit of one booking file, visa case or group in the reporting currency.</summary>
public class TravelContextProfitDto
{
    /// <summary>Booking | VisaCase | Group.</summary>
    public string ContextType { get; set; } = string.Empty;
    public int ContextId { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateTime? TravelDate { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public int? AgentUserId { get; set; }
    public string? AgentName { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    /// <summary>Part of Cost taken from recorded (not yet billed) service costs.</summary>
    public decimal EstimatedCost { get; set; }
    public decimal Commission { get; set; }
    public decimal Margin => Revenue - Cost - Commission;
    public decimal? MarginPercent => Revenue == 0 ? null : Math.Round(Margin / Revenue * 100, 1, MidpointRounding.AwayFromZero);
    public bool HasEstimates => EstimatedCost != 0;
    public int MissingRateCount { get; set; }
}

public class TravelProfitLineDto
{
    /// <summary>Revenue | Cost | Commission.</summary>
    public string Kind { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    /// <summary>In the reporting currency; null when no exchange rate was in force.</summary>
    public decimal? Converted { get; set; }
    public bool Estimated { get; set; }
}

public class TravelContextProfitDetailDto : TravelContextProfitDto
{
    public string ReportingCurrencyCode { get; set; } = string.Empty;
    public IReadOnlyList<TravelProfitLineDto> Lines { get; set; } = [];
}

/// <summary>Rolled up by agent or branch.</summary>
public class TravelProfitGroupDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Contexts { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal Commission { get; set; }
    public decimal Margin => Revenue - Cost - Commission;
    public decimal? MarginPercent => Revenue == 0 ? null : Math.Round(Margin / Revenue * 100, 1, MidpointRounding.AwayFromZero);
}

public class TravelMissingRateDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime EarliestDate { get; set; }
    public int Amounts { get; set; }
}

public class TravelProfitabilityReportDto
{
    public string ReportingCurrencyCode { get; set; } = string.Empty;
    /// <summary>Booking | VisaCase | Group | Agent | Branch.</summary>
    public string Dimension { get; set; } = string.Empty;
    public IReadOnlyList<TravelContextProfitDto> Contexts { get; set; } = [];
    public IReadOnlyList<TravelProfitGroupDto> Groups { get; set; } = [];
    public TravelProfitGroupDto Totals { get; set; } = new();
    public IReadOnlyList<TravelMissingRateDto> MissingRates { get; set; } = [];
}

// ── Facts read by the repository (engine inputs) ────────────────────────

public sealed record TravelContextFact(string ContextType, int ContextId, string Label, DateTime? TravelDate, int? BranchId, string? BranchName,
    int? AgentUserId, string? AgentName);

/// <summary>One issued invoice line attributed to a context, with the invoice totals needed for pro-rata allocation.</summary>
public sealed record TravelRevenueFact(int InvoiceId, string? InvoiceNumber, DateTime IssueDate, string CurrencyCode, decimal InvoiceSubtotal,
    decimal InvoiceDiscount, decimal LineTotal, string? SourceType, string ContextType, int ContextId, string Description);

public sealed record TravelInvoiceCreditFact(int InvoiceId, decimal Amount, DateTime IssuedOn);

/// <summary>A cost attributed to a context: a posted bill line (net of commission) or an estimate for an unbilled service.</summary>
public sealed record TravelCostFact(string ContextType, int ContextId, string CurrencyCode, decimal Amount, DateTime Date, bool Estimated,
    string Reference, string Description, int? BillId);

public sealed record TravelBillCreditFact(int BillId, decimal BillNet, decimal Amount, DateTime IssuedOn, string CurrencyCode);
