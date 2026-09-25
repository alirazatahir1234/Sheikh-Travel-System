namespace SheikhTravelSystem.Application.Features.WhatsApp;

/// <summary>Maps dialing prefixes to WhatsApp account Codes (PK is the only live line).</summary>
public sealed class WhatsAppRoutingOptions
{
    public const string SectionName = "WhatsApp:Routing";

    /// <summary>Fallback account Code when no prefix matches.</summary>
    public string DefaultAccountCode { get; set; } = "PK";

    /// <summary>Dial prefix (digits only) → account Code. Longest prefix wins.</summary>
    public Dictionary<string, string> CountryPrefixes { get; set; } = new(StringComparer.Ordinal)
    {
        ["92"] = "PK",
        // SheikhGo currently operates a single Pakistan WABA — route regional prefixes there.
        ["971"] = "PK",
        ["966"] = "PK",
        ["974"] = "PK",
        ["968"] = "PK",
        ["965"] = "PK",
        ["973"] = "PK",
    };
}
