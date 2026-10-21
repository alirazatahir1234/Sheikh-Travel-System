namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// GT06-family (Jimi EV26R / VG03 / Concox GT06, etc.) operational command payloads.
/// Traccar's native <c>rebootDevice</c> / bare <c>positionSingle</c> return 400 on GT06 —
/// use <c>custom</c> with SMS-style payloads ending in <c>#</c>.
/// Engine cut-off prefers Traccar <c>engineStop</c>/<c>engineResume</c> (protocol-supported)
/// with RELAY payloads for SMS / custom fallback.
/// </summary>
public static class GpsGt06CommandMap
{
    public const string CatalogKeyVg03 = "jimi_vg03";
    public const string CatalogKeyEv26r = "jimi_ev26r";

    public static readonly HashSet<string> Gt06CatalogKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        CatalogKeyEv26r, CatalogKeyVg03, "jimi_gt06",
        "concox_gt06n", "concox_at6", "concox_jm01",
        "sinotrack_st901", "coban_tk103", "eelink_gpt06",
    };

    /// <summary>Ops commands shared by most GT06-family units.</summary>
    public static readonly string[] PrimaryOpsCommandKeys =
    [
        "engineStop",
        "engineResume",
        "positionSingle",
        "restart",
    ];

    /// <summary>EV26R-era diagnostics (not all are documented on JM-VG03).</summary>
    public static readonly string[] DiagnosticCommandKeys =
    [
        "status",
        "check",
        "iccid",
        "imsi",
        "apn",
        "gprsset",
        "server",
    ];

    /// <summary>
    /// JM-VG03 documented Commands-panel set (user manual common SMS):
    /// RESET, STATUS, GPRSSET (+ Network settings query), RELAY stop/resume, URL# position.
    /// Excludes CHECK / ICCID / IMSI / APN query / PARAM.
    /// </summary>
    public static readonly string[] Vg03PanelCommandKeys =
    [
        "engineStop",
        "engineResume",
        "positionSingle",
        "restart",
        "status",
        "gprsset",
        "server",
    ];

    /// <summary>Full EV26R Commands-panel set (ops + full diagnostics).</summary>
    public static readonly string[] Ev26rPanelCommandKeys =
        PrimaryOpsCommandKeys.Concat(DiagnosticCommandKeys).ToArray();

    /// <summary>Default panel = EV26R full set (used when model is unspecified GT06-family).</summary>
    public static readonly string[] PanelCommandKeys = Ev26rPanelCommandKeys;

    public sealed record MappedCommand(
        string CommandKey,
        string Transport,
        string Payload,
        string? TraccarType);

    public static string[] PanelKeysForCatalog(string? catalogKey)
    {
        if (IsVg03Catalog(catalogKey))
            return Vg03PanelCommandKeys;
        return Ev26rPanelCommandKeys;
    }

    public static bool IsVg03Catalog(string? catalogKey) =>
        string.Equals(catalogKey, CatalogKeyVg03, StringComparison.OrdinalIgnoreCase);

    public static bool IsEv26rCatalog(string? catalogKey) =>
        string.Equals(catalogKey, CatalogKeyEv26r, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Map a panel command key to Traccar/SMS payload.
    /// VG03 position uses <c>URL#</c> (documented); EV26R / other GT06 use <c>WHERE#</c>.
    /// </summary>
    public static MappedCommand? TryMap(string commandKey, string? catalogKey = null)
    {
        var positionPayload = IsVg03Catalog(catalogKey) ? "URL#" : "WHERE#";

        return commandKey.ToLowerInvariant() switch
        {
            "restart" => new("restart", "Auto", "RESET#", "custom"),
            "positionsingle" => new("positionSingle", "Auto", positionPayload, "custom"),
            "enginestop" => new("engineStop", "Auto", "RELAY,1#", "engineStop"),
            "engineresume" => new("engineResume", "Auto", "RELAY,0#", "engineResume"),
            "relayon" => new("relayOn", "Auto", "RELAY,1#", "custom"),
            "relayoff" => new("relayOff", "Auto", "RELAY,0#", "custom"),
            "status" => new("status", "Auto", "STATUS#", "custom"),
            "check" => new("check", "Auto", "CHECK#", "custom"),
            "iccid" => new("iccid", "Auto", "ICCID#", "custom"),
            "imsi" => new("imsi", "Auto", "IMSI#", "custom"),
            "apn" => new("apn", "Auto", "APN#", "custom"),
            "gprsset" => new("gprsset", "Auto", "GPRSSET#", "custom"),
            // SERVER is set-only; GPRSSET# returns Server/APN snapshot.
            "server" => new("server", "Auto", "GPRSSET#", "custom"),
            "signal" => new("signal", "Auto", "GPRSSET#", "custom"),
            _ => null
        };
    }

    public static bool LooksLikeGt06Family(
        string? protocol,
        string? model,
        string? vendor,
        string? catalogKey,
        string? name)
    {
        if (!string.IsNullOrWhiteSpace(catalogKey) && Gt06CatalogKeys.Contains(catalogKey))
            return true;

        static bool Has(string? hay, params string[] needles) =>
            !string.IsNullOrWhiteSpace(hay)
            && needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));

        return Has(protocol, "gt06", "jimi")
            || Has(model, "gt06", "vg03", "ev26", "ev26r")
            || Has(vendor, "jimi", "concox", "sinotrack")
            || Has(name, "vg03", "ev26", "jimi");
    }

    /// <summary>Infer VG03 when catalog key is missing but name/model clearly says VG03.</summary>
    public static bool LooksLikeVg03(string? catalogKey, string? model, string? name)
    {
        if (IsVg03Catalog(catalogKey))
            return true;

        static bool Has(string? hay, params string[] needles) =>
            !string.IsNullOrWhiteSpace(hay)
            && needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));

        return Has(model, "vg03", "jm-vg03", "jm_vg03")
            || Has(name, "vg03", "jm-vg03");
    }
}
