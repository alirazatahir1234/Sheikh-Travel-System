namespace SheikhTravelSystem.Application.Features.TravelNexus.Commissions.DTOs;

public class TravelSubAgentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public int ReferredCustomers { get; set; }
}

public record SaveTravelSubAgentDto(string Name, string? Phone, string? Email, string? Notes, bool IsActive);

public class TravelCommissionRuleDto
{
    public int Id { get; set; }
    /// <summary>Staff | SubAgent.</summary>
    public string BeneficiaryType { get; set; } = string.Empty;
    /// <summary>Null = the default rule for every beneficiary of the type.</summary>
    public int? UserId { get; set; }
    public int? SubAgentId { get; set; }
    public string? BeneficiaryName { get; set; }
    /// <summary>All | Flight | Hotel | Visa | Umrah.</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>PercentOfMargin | PercentOfSell.</summary>
    public string Basis { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; }
}

public record SaveTravelCommissionRuleDto(string BeneficiaryType, int? UserId, int? SubAgentId, string Scope, string Basis, decimal Rate,
    DateTime ValidFrom, DateTime? ValidTo, bool IsActive);

/// <summary>Commission one beneficiary earns on one invoice (derived from rules, never stored).</summary>
public class TravelCommissionLineDto
{
    public int InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime IssueDate { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string BeneficiaryType { get; set; } = string.Empty;
    public int BeneficiaryId { get; set; }
    public string BeneficiaryName { get; set; } = string.Empty;
    /// <summary>Invoice currency for %-of-sell rules, reporting currency for %-of-margin rules.</summary>
    public string CurrencyCode { get; set; } = string.Empty;
    /// <summary>Net revenue or margin the rate was applied to.</summary>
    public decimal BasisAmount { get; set; }
    public string Basis { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Accrued (customer still owes) | Payable (invoice settled).</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Set when a margin basis needed an exchange rate that is missing (the line is excluded).</summary>
    public bool MissingRate { get; set; }
}

public class TravelCommissionBalanceDto
{
    public string BeneficiaryType { get; set; } = string.Empty;
    public int BeneficiaryId { get; set; }
    public string BeneficiaryName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Accrued { get; set; }
    public decimal Payable { get; set; }
    public decimal Paid { get; set; }
    /// <summary>Payable − Paid; negative when a credit note reduced commission already paid out.</summary>
    public decimal Balance => Payable - Paid;
}

public class TravelCommissionPayoutDto
{
    public int Id { get; set; }
    public string PayoutNumber { get; set; } = string.Empty;
    public string BeneficiaryType { get; set; } = string.Empty;
    public int BeneficiaryId { get; set; }
    public string BeneficiaryName { get; set; } = string.Empty;
    public DateTime PaidOn { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public record RecordTravelCommissionPayoutDto(Guid IdempotencyKey, string BeneficiaryType, int BeneficiaryId, DateTime PaidOn, string Method,
    string? Reference, decimal Amount, string CurrencyCode, string? Notes);

public record UpdateTravelInvoiceAttributionDto(int? AgentUserId, int? SubAgentId);

// ── Internal facts ──────────────────────────────────────────────────────

/// <summary>An issued invoice with someone to pay commission to.</summary>
public sealed record TravelAttributedInvoiceFact(int InvoiceId, string? InvoiceNumber, DateTime IssueDate, int CustomerId, string CustomerName,
    string CurrencyCode, decimal Balance, int? AgentUserId, string? AgentName, int? SubAgentId, string? SubAgentName);
