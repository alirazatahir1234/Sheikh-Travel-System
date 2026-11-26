namespace SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;

/// <summary>One lead time of a rule: when it fires and through which channels.</summary>
public class TravelReminderLeadDto
{
    public int LeadDays { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool StaffInApp { get; set; }
    public bool StaffEmail { get; set; }
    public bool CustomerEmail { get; set; }
    public bool CustomerSms { get; set; }
    public bool CustomerWhatsApp { get; set; }
}

/// <summary>A catalogue rule merged with the tenant's rows (defaults when the tenant has none).</summary>
public class TravelReminderRuleDto
{
    public string RuleType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Traveler | VisaCase | FlightBooking | HotelBooking | Invoice | Enquiry | Group.</summary>
    public string EntityType { get; set; } = string.Empty;
    /// <summary>Upcoming (fires before a date) | Overdue (nudges while a date is past) | Event (fires once per change).</summary>
    public string Kind { get; set; } = string.Empty;
    public bool SupportsStaff { get; set; }
    public bool SupportsCustomer { get; set; }
    /// <summary>True when the tenant saved its own rows (false = catalogue defaults shown).</summary>
    public bool IsCustomised { get; set; }
    public IReadOnlyList<TravelReminderLeadDto> Leads { get; set; } = [];
    public string? CustomerTemplate { get; set; }
    public IReadOnlyList<string> Placeholders { get; set; } = [];
}

public record SaveTravelReminderRuleDto(IReadOnlyList<TravelReminderLeadDto> Leads, string? CustomerTemplate);

public class TravelReminderLogItemDto
{
    public long Id { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public int LeadDays { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string ContextLabel { get; set; } = string.Empty;
    public DateTime DueAt { get; set; }
    /// <summary>Staff | Customer.</summary>
    public string Audience { get; set; } = string.Empty;
    public int? RecipientUserId { get; set; }
    public string? RecipientUserName { get; set; }
    public int? RecipientCustomerId { get; set; }
    public string? RecipientCustomerName { get; set; }
    public string Channels { get; set; } = string.Empty;
    /// <summary>Pending | Sent | Skipped | Failed.</summary>
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public int? NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TravelReminderRunResultDto
{
    public int Candidates { get; set; }
    public int Sent { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    /// <summary>Customer reminders held because the tenant is in quiet hours (they fire on a later pass).</summary>
    public int Held { get; set; }
}

// ── Internal facts ──────────────────────────────────────────────────────

/// <summary>A tenant's stored rule row.</summary>
public sealed record TravelReminderRuleRow(string RuleType, int LeadDays, bool IsEnabled, bool StaffInApp, bool StaffEmail, bool CustomerEmail,
    bool CustomerSms, bool CustomerWhatsApp, string? CustomerTemplate);

/// <summary>
/// Something a rule may remind about: the entity, when its trigger falls due, who is responsible and how to reach the customer.
/// <see cref="Stamp"/> distinguishes repeat triggers of the same entity (a status change id, a moved date) and goes into the dedupe key.
/// </summary>
public sealed record TravelReminderCandidate(string EntityType, int EntityId, string ContextLabel, DateTime DueAt, string? Stamp,
    int? StaffUserId, int? CustomerId, string? CustomerName, string? CustomerPhone, string? CustomerEmail, string Reference,
    decimal? Amount, string? CurrencyCode, string? Detail, int? BranchId);

public sealed record TravelReminderClaim(string RuleType, int LeadDays, TravelReminderCandidate Candidate, string Audience, string DedupeKey,
    IReadOnlyList<string> Channels, string Title, string Body);

public sealed record TravelReminderLogFilter(string? RuleType, string? Status, string? Audience, DateTime? From, DateTime? To);

/// <summary>A deadline for the dashboard's upcoming list (same source as the reminders).</summary>
public class TravelUpcomingItemDto
{
    public string RuleType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public DateTime DueAt { get; set; }
    public int? StaffUserId { get; set; }
    public string? CustomerName { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
}
