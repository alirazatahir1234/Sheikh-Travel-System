namespace SheikhTravelSystem.Application.Features.WhatsApp;

/// <summary>Maps dialing prefixes to WhatsApp account Codes (UAE, PK, …).</summary>
public sealed class WhatsAppRoutingOptions
{
    public const string SectionName = "WhatsApp:Routing";

    /// <summary>Fallback account Code when no prefix matches.</summary>
    public string DefaultAccountCode { get; set; } = "UAE";

    /// <summary>Dial prefix (digits only) → account Code. Longest prefix wins.</summary>
    public Dictionary<string, string> CountryPrefixes { get; set; } = new(StringComparer.Ordinal)
    {
        ["92"] = "PK",
        ["971"] = "UAE",
        ["966"] = "UAE",
        ["974"] = "UAE",
        ["968"] = "UAE",
        ["965"] = "UAE",
        ["973"] = "UAE",
    };
}
