namespace SheikhTravelSystem.Application.Features.WhatsApp.Bot;

public static class WhatsAppDemoBotStates
{
    public const string Idle = "Idle";
    public const string AwaitFleetType = "AwaitFleetType";
    public const string AwaitFleetSize = "AwaitFleetSize";
    public const string AwaitChallenge = "AwaitChallenge";
    public const string Completed = "Completed";
    public const string HandedOff = "HandedOff";

    public static string Normalize(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return Idle;
        var t = state.Trim();
        if (t.Equals("Active", StringComparison.OrdinalIgnoreCase)) return Idle;
        return t;
    }
}

public static class WhatsAppDemoBotOptions
{
    public static readonly IReadOnlyList<string> FleetTypes =
    [
        "Car Rental",
        "Transportation",
        "Logistics",
        "Travel / Tourism",
        "Other"
    ];

    public static readonly IReadOnlyList<string> FleetSizes =
    [
        "1-10",
        "11-25",
        "26-50",
        "51-100",
        "100+"
    ];

    public static readonly IReadOnlyList<string> Challenges =
    [
        "GPS Tracking",
        "Driver Management",
        "Trips & Dispatch",
        "Fuel Management",
        "Maintenance",
        "Bookings",
        "Reporting",
        "Other"
    ];

    public const string ThankYouMessage =
        "Thanks. Our SheikhGo team can arrange a short demo. A team member will contact you shortly.";

    public const string InvalidOptionMessage =
        "Please choose one of the options.";

    public const string AskFleetType = "What type of fleet do you operate?";
    public const string AskFleetSize = "Approximately how many vehicles do you manage?";
    public const string AskChallenge = "What is your biggest challenge today?";

    /// <summary>Match option by exact label (case-insensitive) or 1-based index.</summary>
    public static string? MatchOption(IReadOnlyList<string> options, string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || options.Count == 0) return null;
        var raw = input.Trim();

        if (int.TryParse(raw, out var index) && index >= 1 && index <= options.Count)
            return options[index - 1];

        // Interactive list id: fleet_type_1 etc.
        if (raw.StartsWith("opt_", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(raw.AsSpan(4), out var optIdx)
            && optIdx >= 1 && optIdx <= options.Count)
            return options[optIdx - 1];

        return options.FirstOrDefault(o => o.Equals(raw, StringComparison.OrdinalIgnoreCase));
    }

    public static string FormatNumberedList(string prompt, IReadOnlyList<string> options)
    {
        var lines = new List<string> { prompt, "" };
        for (var i = 0; i < options.Count; i++)
            lines.Add($"{i + 1}. {options[i]}");
        return string.Join('\n', lines);
    }
}

public sealed record WhatsAppDemoBotReply(
    string NextState,
    string TextBody,
    IReadOnlyList<string>? ListOptions,
    string? ListButtonLabel,
    string? StoredFleetType,
    string? StoredFleetSize,
    string? StoredChallenge,
    bool QualifyLead);

public sealed record WhatsAppDemoBotInput(
    string CurrentState,
    bool IsBotEnabled,
    string? UserText);

public interface IWhatsAppDemoQualificationBot
{
    WhatsAppDemoBotReply? Process(WhatsAppDemoBotInput input);
}

/// <summary>Deterministic DEMO qualification state machine — no AI.</summary>
public sealed class WhatsAppDemoQualificationBot : IWhatsAppDemoQualificationBot
{
    public WhatsAppDemoBotReply? Process(WhatsAppDemoBotInput input)
    {
        if (!input.IsBotEnabled)
            return null;

        var state = WhatsAppDemoBotStates.Normalize(input.CurrentState);
        var text = input.UserText?.Trim() ?? "";

        return state switch
        {
            WhatsAppDemoBotStates.Idle or WhatsAppDemoBotStates.Completed => ProcessIdle(text),
            WhatsAppDemoBotStates.AwaitFleetType => ProcessFleetType(text),
            WhatsAppDemoBotStates.AwaitFleetSize => ProcessFleetSize(text),
            WhatsAppDemoBotStates.AwaitChallenge => ProcessChallenge(text),
            _ => ProcessIdle(text)
        };
    }

    private static WhatsAppDemoBotReply? ProcessIdle(string text)
    {
        if (!text.Equals("DEMO", StringComparison.OrdinalIgnoreCase))
            return null;

        return new WhatsAppDemoBotReply(
            WhatsAppDemoBotStates.AwaitFleetType,
            WhatsAppDemoBotOptions.FormatNumberedList(
                WhatsAppDemoBotOptions.AskFleetType, WhatsAppDemoBotOptions.FleetTypes),
            WhatsAppDemoBotOptions.FleetTypes,
            "Fleet type",
            null, null, null,
            QualifyLead: false);
    }

    private static WhatsAppDemoBotReply ProcessFleetType(string text)
    {
        var match = WhatsAppDemoBotOptions.MatchOption(WhatsAppDemoBotOptions.FleetTypes, text);
        if (match is null)
        {
            return new WhatsAppDemoBotReply(
                WhatsAppDemoBotStates.AwaitFleetType,
                WhatsAppDemoBotOptions.InvalidOptionMessage + "\n\n"
                    + WhatsAppDemoBotOptions.FormatNumberedList(
                        WhatsAppDemoBotOptions.AskFleetType, WhatsAppDemoBotOptions.FleetTypes),
                WhatsAppDemoBotOptions.FleetTypes,
                "Fleet type",
                null, null, null,
                false);
        }

        return new WhatsAppDemoBotReply(
            WhatsAppDemoBotStates.AwaitFleetSize,
            WhatsAppDemoBotOptions.FormatNumberedList(
                WhatsAppDemoBotOptions.AskFleetSize, WhatsAppDemoBotOptions.FleetSizes),
            WhatsAppDemoBotOptions.FleetSizes,
            "Fleet size",
            match, null, null,
            false);
    }

    private static WhatsAppDemoBotReply ProcessFleetSize(string text)
    {
        var match = WhatsAppDemoBotOptions.MatchOption(WhatsAppDemoBotOptions.FleetSizes, text);
        if (match is null)
        {
            return new WhatsAppDemoBotReply(
                WhatsAppDemoBotStates.AwaitFleetSize,
                WhatsAppDemoBotOptions.InvalidOptionMessage + "\n\n"
                    + WhatsAppDemoBotOptions.FormatNumberedList(
                        WhatsAppDemoBotOptions.AskFleetSize, WhatsAppDemoBotOptions.FleetSizes),
                WhatsAppDemoBotOptions.FleetSizes,
                "Fleet size",
                null, null, null,
                false);
        }

        return new WhatsAppDemoBotReply(
            WhatsAppDemoBotStates.AwaitChallenge,
            WhatsAppDemoBotOptions.FormatNumberedList(
                WhatsAppDemoBotOptions.AskChallenge, WhatsAppDemoBotOptions.Challenges),
            WhatsAppDemoBotOptions.Challenges,
            "Challenge",
            null, match, null,
            false);
    }

    private static WhatsAppDemoBotReply ProcessChallenge(string text)
    {
        var match = WhatsAppDemoBotOptions.MatchOption(WhatsAppDemoBotOptions.Challenges, text);
        if (match is null)
        {
            return new WhatsAppDemoBotReply(
                WhatsAppDemoBotStates.AwaitChallenge,
                WhatsAppDemoBotOptions.InvalidOptionMessage + "\n\n"
                    + WhatsAppDemoBotOptions.FormatNumberedList(
                        WhatsAppDemoBotOptions.AskChallenge, WhatsAppDemoBotOptions.Challenges),
                WhatsAppDemoBotOptions.Challenges,
                "Challenge",
                null, null, null,
                false);
        }

        return new WhatsAppDemoBotReply(
            WhatsAppDemoBotStates.Completed,
            WhatsAppDemoBotOptions.ThankYouMessage,
            null,
            null,
            null, null, match,
            QualifyLead: true);
    }
}
