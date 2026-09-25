using System.Text.Json;
using System.Text.Json.Serialization;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Bot;

public static class WhatsAppSelfServiceStates
{
    public const string MainMenu = "MainMenu";
    public const string AwaitBookingPickup = "AwaitBookingPickup";
    public const string AwaitBookingDestination = "AwaitBookingDestination";
    public const string AwaitBookingDate = "AwaitBookingDate";
    public const string AwaitBookingTime = "AwaitBookingTime";
    public const string AwaitBookingVehicleType = "AwaitBookingVehicleType";
    public const string AwaitBookingPassengers = "AwaitBookingPassengers";
    public const string AwaitBookingNotes = "AwaitBookingNotes";
    public const string AwaitTripRef = "AwaitTripRef";
    public const string Invoices = "Invoices";
    public const string HumanHandoff = "HumanHandoff";

    public static bool IsSelfService(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return false;
        var s = state.Trim();
        return s is MainMenu or AwaitBookingPickup or AwaitBookingDestination or AwaitBookingDate
            or AwaitBookingTime or AwaitBookingVehicleType or AwaitBookingPassengers or AwaitBookingNotes
            or AwaitTripRef or Invoices or HumanHandoff
            || s.StartsWith("AwaitBooking", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDemoState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return false;
        var s = WhatsAppDemoBotStates.Normalize(state);
        return s is WhatsAppDemoBotStates.AwaitFleetType
            or WhatsAppDemoBotStates.AwaitFleetSize
            or WhatsAppDemoBotStates.AwaitChallenge;
    }
}

public static class WhatsAppSelfServiceMenu
{
    public const string Book = "ss_book";
    public const string Track = "ss_track";
    public const string Invoices = "ss_invoices";
    public const string Agent = "ss_agent";

    public const string PayPrefix = "ss_pay_";
    public const string ViewPrefix = "ss_view_";
    public const string MenuAgain = "ss_menu";
    public const string SkipNotes = "ss_skip_notes";

    public static readonly IReadOnlyList<WhatsAppInteractiveRow> MainMenuRows =
    [
        new(Book, "Book a Vehicle"),
        new(Track, "Track My Trip"),
        new(Invoices, "My Invoices"),
        new(Agent, "Talk to an Agent")
    ];

    public static readonly IReadOnlyList<string> VehicleTypes =
    [
        "Economy Sedan",
        "Business Sedan",
        "SUV",
        "Van / Hiace",
        "Other"
    ];

    public static string? MatchMenuId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var raw = input.Trim();

        if (raw.Equals(Book, StringComparison.OrdinalIgnoreCase)
            || raw.Equals("Book a Vehicle", StringComparison.OrdinalIgnoreCase)
            || raw is "1")
            return Book;

        if (raw.Equals(Track, StringComparison.OrdinalIgnoreCase)
            || raw.Equals("Track My Trip", StringComparison.OrdinalIgnoreCase)
            || raw is "2")
            return Track;

        if (raw.Equals(Invoices, StringComparison.OrdinalIgnoreCase)
            || raw.Equals("My Invoices", StringComparison.OrdinalIgnoreCase)
            || raw is "3")
            return Invoices;

        if (raw.Equals(Agent, StringComparison.OrdinalIgnoreCase)
            || raw.Equals("Talk to an Agent", StringComparison.OrdinalIgnoreCase)
            || raw is "4")
            return Agent;

        if (raw.Equals(MenuAgain, StringComparison.OrdinalIgnoreCase)
            || raw.Equals("MENU", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("HI", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("HELP", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("HELLO", StringComparison.OrdinalIgnoreCase))
            return MenuAgain;

        return null;
    }

    public static bool IsMenuEntryKeyword(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var t = input.Trim();
        return t.Equals("MENU", StringComparison.OrdinalIgnoreCase)
               || t.Equals("HI", StringComparison.OrdinalIgnoreCase)
               || t.Equals("HELP", StringComparison.OrdinalIgnoreCase)
               || t.Equals("HELLO", StringComparison.OrdinalIgnoreCase)
               || t.Equals("START", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class WhatsAppBotSessionData
{
    [JsonPropertyName("pickup")]
    public string? Pickup { get; set; }

    [JsonPropertyName("destination")]
    public string? Destination { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("vehicleType")]
    public string? VehicleType { get; set; }

    [JsonPropertyName("passengers")]
    public int? Passengers { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("lastInvoiceBookingId")]
    public int? LastInvoiceBookingId { get; set; }

    public static WhatsAppBotSessionData Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new WhatsAppBotSessionData();
        try
        {
            return JsonSerializer.Deserialize<WhatsAppBotSessionData>(json) ?? new WhatsAppBotSessionData();
        }
        catch
        {
            return new WhatsAppBotSessionData();
        }
    }

    public string ToJson()
        => JsonSerializer.Serialize(this);
}

public sealed record WhatsAppSelfServiceReply(
    string NextState,
    string TextBody,
    bool DisableBot = false,
    IReadOnlyList<WhatsAppInteractiveRow>? ListRows = null,
    string? ListButtonLabel = null,
    IReadOnlyList<WhatsAppInteractiveRow>? ReplyButtons = null,
    WhatsAppBotSessionData? Session = null,
    bool ClearSession = false);

public sealed record WhatsAppSelfServiceTripSummary(
    int TripId,
    string? TripNumber,
    int? BookingId,
    string? BookingNumber,
    string StatusLabel,
    string? DriverFirstName,
    string? VehicleDescription,
    string? PlateNumber,
    string? TrackingUrl);

public sealed record WhatsAppSelfServiceInvoiceItem(
    int BookingId,
    string BookingNumber,
    DateTime PickupTime,
    decimal TotalAmount,
    decimal PaidAmount,
    string Currency);

public static class WhatsAppSelfServiceCopy
{
    public const string Welcome =
        "Welcome to SheikhGo\n\nHow can we help you?";

    public const string AskPickup = "Where should we pick you up?";

    public const string AskTripRef =
        "Please send your booking reference or trip reference (for example SG-BK-10245).";

    public const string TripNotFound =
        "We could not find an active trip for that reference on this WhatsApp number.";

    public const string AgentHandoff =
        "Sure. A SheikhGo team member will assist you shortly.";

    public const string UnknownMenu =
        "Please choose an option from the menu, or type MENU.";

    public const string PayComingSoon =
        "Secure payment links will be available shortly. A team member can also help you pay.";

    public const string NoInvoices =
        "We could not find invoices linked to this WhatsApp number.";
}
