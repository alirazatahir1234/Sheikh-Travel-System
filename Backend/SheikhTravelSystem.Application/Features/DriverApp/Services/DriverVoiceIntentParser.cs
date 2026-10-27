namespace SheikhTravelSystem.Application.Features.DriverApp.Services;

public enum DriverVoiceIntent
{
    Message = 0,
    Alert = 1,
    Sos = 2,
}

/// <summary>
/// Lightweight keyword routing for EN / AR / DE voice transcripts (no external NLP).
/// </summary>
public static class DriverVoiceIntentParser
{
    private static readonly string[] SosTokens =
    [
        "sos", "emergency", "help me", "panic", "mayday",
        "طوارئ", "نجدة", "النجدة", "مساعدة", "سوس",
        "notfall", "notruf", "hilfe", "panik",
    ];

    private static readonly string[] AlertTokens =
    [
        "alert", "accident", "breakdown", "flat tire", "traffic", "delay", "stuck", "problem",
        "تنبيه", "حادث", "عطل", "تأخير", "مشكلة", "ازدحام",
        "unfall", "panne", "reifen", "stau", "verzögerung", "problem", "warnung",
    ];

    public static DriverVoiceIntent Parse(string? transcript, string? requestedIntent)
    {
        if (!string.IsNullOrWhiteSpace(requestedIntent))
        {
            var r = requestedIntent.Trim().ToLowerInvariant();
            if (r is "sos" or "emergency") return DriverVoiceIntent.Sos;
            if (r is "alert" or "warning") return DriverVoiceIntent.Alert;
            if (r is "message" or "dispatch" or "msg") return DriverVoiceIntent.Message;
        }

        var t = (transcript ?? string.Empty).Trim().ToLowerInvariant();
        if (t.Length == 0) return DriverVoiceIntent.Message;

        if (ContainsAny(t, SosTokens)) return DriverVoiceIntent.Sos;
        if (ContainsAny(t, AlertTokens)) return DriverVoiceIntent.Alert;
        return DriverVoiceIntent.Message;
    }

    private static bool ContainsAny(string haystack, IEnumerable<string> needles)
    {
        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
