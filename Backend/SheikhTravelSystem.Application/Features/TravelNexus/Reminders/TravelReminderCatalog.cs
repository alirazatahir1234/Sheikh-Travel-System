using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Reminders;

public enum TravelReminderKind
{
    /// <summary>Fires once per lead time while <c>now ≤ due ≤ now + lead</c>.</summary>
    Upcoming,
    /// <summary>Fires while the date is past; keyed by day or ISO week so it nudges again, and stops as soon as the trigger clears.</summary>
    Overdue,
    /// <summary>Fires once per occurrence (a status change); the candidate's stamp makes each occurrence distinct.</summary>
    Event
}

/// <summary>A built-in reminder rule: what it watches, how it fires and the tenant-independent defaults.</summary>
public sealed record TravelReminderDefinition(string RuleType, string Name, string Description, string EntityType, TravelReminderKind Kind,
    bool SupportsStaff, bool SupportsCustomer, int[] DefaultLeads, bool DefaultCustomerEmail, bool WeeklyNudge = false)
{
    public TravelReminderLeadDto DefaultLead(int leadDays) => new()
    {
        LeadDays = leadDays, IsEnabled = true, StaffInApp = SupportsStaff, StaffEmail = false,
        CustomerEmail = SupportsCustomer && DefaultCustomerEmail, CustomerSms = false, CustomerWhatsApp = false
    };
}

/// <summary>The fixed catalogue of reminder rules. Tenants tune lead times, channels and the customer text; they cannot add rule types.</summary>
public static class TravelReminderCatalog
{
    public static readonly IReadOnlyList<string> Placeholders = ["{{Name}}", "{{Reference}}", "{{Date}}", "{{Amount}}", "{{Detail}}"];

    public static readonly IReadOnlyList<TravelReminderDefinition> All =
    [
        new("PassportExpiry", "Passport expiry", "A traveler's passport expires soon.", "Traveler", TravelReminderKind.Upcoming, true, true, [90, 30], true),
        new("VisaAppointment", "Visa appointment", "A visa appointment is coming up.", "VisaCase", TravelReminderKind.Upcoming, true, true, [3, 1], true),
        new("MissingDocuments", "Missing documents", "Travel is near and mandatory documents are still outstanding.", "VisaCase", TravelReminderKind.Upcoming, true, true, [30, 14], true),
        new("VisaDecisionOverdue", "Visa decision overdue", "A submitted case has passed its expected decision date.", "VisaCase", TravelReminderKind.Overdue, true, false, [0], false, WeeklyNudge: true),
        new("VisaStatusChanged", "Visa status update", "The customer is told when a case is submitted or decided.", "VisaCase", TravelReminderKind.Event, false, true, [0], true),
        new("TicketingDeadline", "Ticketing deadline", "A held flight must be ticketed soon.", "FlightBooking", TravelReminderKind.Upcoming, true, false, [2, 1], false),
        new("FlightDeparture", "Flight departure", "A confirmed or ticketed flight departs soon.", "FlightBooking", TravelReminderKind.Upcoming, true, true, [3, 1], true),
        new("HotelCheckIn", "Hotel check-in", "A confirmed hotel stay starts soon.", "HotelBooking", TravelReminderKind.Upcoming, true, true, [2], true),
        new("InvoiceDue", "Payment due", "An invoice or installment falls due soon.", "Invoice", TravelReminderKind.Upcoming, true, true, [7, 1], true),
        new("InvoiceOverdue", "Payment overdue", "An invoice is past due with a balance (weekly nudge).", "Invoice", TravelReminderKind.Overdue, true, true, [0], true, WeeklyNudge: true),
        new("EnquiryFollowUp", "Enquiry follow-up", "A follow-up is due today or was missed (daily nudge).", "Enquiry", TravelReminderKind.Overdue, true, false, [0], false),
        new("GroupDeparture", "Group departure", "An Umrah group departs soon.", "Group", TravelReminderKind.Upcoming, true, true, [7, 1], false),
    ];

    public static TravelReminderDefinition? Find(string? ruleType) => All.FirstOrDefault(d => d.RuleType == ruleType);

    public static bool IsKnown(string? ruleType) => Find(ruleType) is not null;
}
