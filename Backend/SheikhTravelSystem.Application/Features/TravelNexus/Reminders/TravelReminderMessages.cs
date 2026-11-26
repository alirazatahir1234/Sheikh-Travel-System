using System.Globalization;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Reminders;

/// <summary>
/// Reminder texts. Money always carries its ISO code; dates are rendered in the tenant's time zone; no passport or CNIC number
/// ever appears (the repository already masks the passport in the context label). A tenant's customer template replaces the
/// built-in customer body; its placeholders are substituted, nothing else is interpreted.
/// </summary>
public static class TravelReminderMessages
{
    public sealed record Text(string Title, string Body);

    public static Text Build(TravelReminderDefinition d, TravelReminderCandidate c, string audience, string? customerTemplate, TimeZoneInfo zone)
    {
        var when = When(d, c.DueAt, zone);
        var amount = c.Amount is decimal a && c.CurrencyCode is not null ? Money(a, c.CurrencyCode) : null;
        var name = c.CustomerName ?? "Customer";
        var title = d.RuleType switch
        {
            "PassportExpiry" => $"Passport expires {when}",
            "VisaAppointment" => $"Visa appointment {when}",
            "MissingDocuments" => "Documents still missing",
            "VisaDecisionOverdue" => "Visa decision overdue",
            "VisaStatusChanged" => $"Visa case {c.Reference}: {c.Detail}",
            "TicketingDeadline" => $"Ticketing deadline {when}",
            "FlightDeparture" => $"Flight departs {when}",
            "HotelCheckIn" => $"Hotel check-in {when}",
            "InvoiceDue" => $"Payment due {when}",
            "InvoiceOverdue" => "Payment overdue",
            "EnquiryFollowUp" => "Follow-up due",
            "GroupDeparture" => $"Group departs {when}",
            _ => d.Name
        };

        if (audience == "Customer")
        {
            var body = string.IsNullOrWhiteSpace(customerTemplate) ? DefaultCustomerBody(d, c, when, amount, name) : customerTemplate!;
            return new Text(title, Substitute(body, name, c.Reference, when, amount, c.Detail));
        }
        return new Text(title, StaffBody(d, c, when, amount, name));
    }

    private static string StaffBody(TravelReminderDefinition d, TravelReminderCandidate c, string when, string? amount, string name) => d.RuleType switch
    {
        "PassportExpiry" => $"{c.ContextLabel} ({name}) expires {when}.",
        "VisaAppointment" => $"{c.Reference} · {name}: appointment {when}{Detail(c)}.",
        "MissingDocuments" => $"{c.Reference} · {name}: {c.Detail ?? "mandatory documents outstanding"}; travel {when}.",
        "VisaDecisionOverdue" => $"{c.Reference} · {name}: decision expected {when}, still {c.Detail ?? "pending"}.",
        "TicketingDeadline" => $"{c.ContextLabel}: ticketing time limit {when}.",
        "FlightDeparture" => $"{c.ContextLabel} ({name}) departs {when}.",
        "HotelCheckIn" => $"{c.ContextLabel} ({name}) checks in {when}.",
        "InvoiceDue" => $"{c.Reference} · {name}: {amount ?? "balance"} due {when}.",
        "InvoiceOverdue" => $"{c.Reference} · {name}: {amount ?? "balance"} was due {when}.",
        "EnquiryFollowUp" => $"{c.Reference} · {name}: follow-up was due {when}{Detail(c)}.",
        "GroupDeparture" => $"{c.ContextLabel} departs {when}{Detail(c)}.",
        _ => c.ContextLabel
    };

    private static string DefaultCustomerBody(TravelReminderDefinition d, TravelReminderCandidate c, string when, string? amount, string name) => d.RuleType switch
    {
        "PassportExpiry" => $"Dear {name}, the passport {c.Detail ?? "on file"} expires {when}. Please renew it before travelling.",
        "VisaAppointment" => $"Dear {name}, your visa appointment for {c.Reference} is {when}{Detail(c)}. Please bring your original documents.",
        "MissingDocuments" => $"Dear {name}, we still need documents for your visa case {c.Reference} ({c.Detail ?? "see your checklist"}) before travel {when}.",
        "VisaStatusChanged" => $"Dear {name}, your visa case {c.Reference} is now: {c.Detail}.",
        "FlightDeparture" => $"Dear {name}, your flight {c.Reference} departs {when}. Please be at the airport three hours before departure.",
        "HotelCheckIn" => $"Dear {name}, your stay at {c.Detail ?? "the hotel"} ({c.Reference}) starts {when}.",
        "InvoiceDue" => $"Dear {name}, {amount ?? "your balance"} on invoice {c.Reference} is due {when}. Thank you.",
        "InvoiceOverdue" => $"Dear {name}, {amount ?? "your balance"} on invoice {c.Reference} was due {when}. Please arrange payment.",
        "GroupDeparture" => $"Dear {name}, your group {c.Reference} departs {when}{Detail(c)}.",
        _ => $"Dear {name}, a reminder about {c.Reference}: {d.Name.ToLowerInvariant()} {when}."
    };

    private static string Detail(TravelReminderCandidate c) => string.IsNullOrWhiteSpace(c.Detail) ? string.Empty : $" · {c.Detail}";

    public static string Substitute(string template, string name, string reference, string when, string? amount, string? detail)
        => template.Replace("{{Name}}", name).Replace("{{Reference}}", reference).Replace("{{Date}}", when)
            .Replace("{{Amount}}", amount ?? string.Empty).Replace("{{Detail}}", detail ?? string.Empty).Trim();

    /// <summary>"on 12 Mar 2027" for dates, "on 12 Mar 2027 at 14:30" when the trigger has a time of day.</summary>
    public static string When(TravelReminderDefinition d, DateTime dueAt, TimeZoneInfo zone)
    {
        var hasTime = d.RuleType is "VisaAppointment" or "TicketingDeadline" or "FlightDeparture" or "EnquiryFollowUp" or "VisaStatusChanged";
        var local = hasTime ? TravelReminderRules.ToLocal(dueAt, zone) : dueAt;
        return hasTime
            ? $"on {local.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} at {local.ToString("HH:mm", CultureInfo.InvariantCulture)}"
            : $"on {local.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
    }

    public static string Money(decimal amount, string currency)
        => $"{amount.ToString("#,##0.00", CultureInfo.InvariantCulture)} {currency}";
}
