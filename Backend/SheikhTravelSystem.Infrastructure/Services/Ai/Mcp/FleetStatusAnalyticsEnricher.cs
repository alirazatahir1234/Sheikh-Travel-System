using System.Globalization;
using System.Text.Json;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

/// <summary>
/// Maps live <c>get_fleet_status</c> MCP JSON into the ERP rich-widget contract
/// (<c>responseType=fleet_status_analytics</c>) so the assistant UI can render KPIs,
/// distribution, vehicle table, insights, evidence, and follow-ups — without inventing data.
/// </summary>
public static class FleetStatusAnalyticsEnricher
{
    public const string ResponseType = "fleet_status_analytics";

    private sealed record VehicleRow(
        int VehicleId,
        string VehicleName,
        string RegistrationNumber,
        string GpsBucket,
        string StatusLabel,
        string StatusTone,
        string? VehicleStatusName,
        double? Speed,
        string? LastUpdate,
        bool? Ignition);

    private readonly record struct Summary(
        int Total, int Online, int Offline, int Moving, int Idle, int Parked, int Sos, int NeverSeen, int Unknown);

    /// <summary>
    /// Returns enriched analytics JSON when <paramref name="raw"/> is a fleet-status payload
    /// (typed or raw MCP shape). Otherwise returns null.
    /// </summary>
    public static string? TryEnrich(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (root.TryGetProperty("responseType", out var rt)
                && rt.ValueKind == JsonValueKind.String
                && string.Equals(rt.GetString(), ResponseType, StringComparison.OrdinalIgnoreCase))
            {
                return raw;
            }

            if (!TryReadSummary(root, out var summary))
                return null;

            var offlineStale = ReadInt(root, "offlineStaleMinutes") ?? 30;
            var movingThreshold = ReadDouble(root, "movingThresholdKmh") ?? 10;
            var tenantId = ReadInt(root, "tenantId");
            var note = ReadString(root, "note");
            var warnings = ReadStringArray(root, "warnings");
            var vehicles = ReadVehicles(root);

            var total = summary.Total;
            var online = summary.Online;
            var offline = summary.Offline;
            var moving = summary.Moving;
            var idle = summary.Idle;
            var parked = summary.Parked;
            var sos = summary.Sos;
            var neverSeen = summary.NeverSeen;
            var unknown = summary.Unknown;

            var onlinePct = total > 0 ? Math.Round(online * 1000.0 / total) / 10.0 : 0;
            var offlinePct = total > 0 ? Math.Round(offline * 1000.0 / total) / 10.0 : 0;

            var answer = total == 0
                ? "No vehicles were returned for this tenant’s live fleet status."
                : $"Fleet status: {online} of {total} vehicles online ({onlinePct}%), {offline} offline ({offlinePct}%), {moving} moving, {parked} parked, {idle} idle"
                  + (sos > 0 ? $", {sos} SOS." : ".");

            var context =
                $"Live GPS buckets from VehicleCurrentLocation (offline after {offlineStale} min; moving ≥ {movingThreshold} km/h)."
                + (tenantId is > 0 ? " Tenant scope applied." : "");

            var interpretation = BuildInterpretation(summary, onlinePct, offlinePct);
            var insights = BuildInsights(summary, onlinePct, vehicles);
            var actions = BuildActions(summary);
            var followUps = BuildFollowUps(summary);
            var evidence = BuildEvidence(note, offlineStale, movingThreshold, vehicles, warnings);

            var vehicleDtos = vehicles.Select(v => new
            {
                vehicleId = v.VehicleId,
                vehicleName = v.VehicleName,
                registrationNumber = v.RegistrationNumber,
                gpsBucket = v.GpsBucket,
                statusLabel = v.StatusLabel,
                statusTone = v.StatusTone,
                vehicleStatusName = v.VehicleStatusName,
                speed = v.Speed,
                lastUpdate = v.LastUpdate,
                ignition = v.Ignition
            }).ToList();

            var payload = new
            {
                responseType = ResponseType,
                source = "get_fleet_status",
                tenantId,
                offlineStaleMinutes = offlineStale,
                movingThresholdKmh = movingThreshold,
                summary = new
                {
                    total,
                    online,
                    offline,
                    moving,
                    idle,
                    parked,
                    sos,
                    neverSeen,
                    unknown,
                    onlinePercent = onlinePct,
                    offlinePercent = offlinePct
                },
                answer,
                context,
                interpretation,
                nextAction = actions.FirstOrDefault() ?? "Open Live Map to verify positions.",
                kpis = new object[]
                {
                    Kpi("online", "Online", online, "count", "wifi", "green", onlinePct),
                    Kpi("offline", "Offline", offline, "count", "wifi_off", "red", offlinePct),
                    Kpi("parked", "Parked", parked, "count", "local_parking", "purple",
                        total > 0 ? Math.Round(parked * 1000.0 / total) / 10.0 : 0),
                    Kpi("idle", "Idle", idle, "count", "pause_circle", "amber",
                        total > 0 ? Math.Round(idle * 1000.0 / total) / 10.0 : 0)
                },
                statusDistribution = new object[]
                {
                    Slice("online", "Online", online),
                    Slice("offline", "Offline", offline),
                    Slice("parked", "Parked", parked),
                    Slice("moving", "Moving", moving),
                    Slice("idle", "Idle", idle),
                    Slice("sos", "SOS", sos),
                    Slice("neverSeen", "Never Seen", neverSeen),
                    Slice("unknown", "Unknown", unknown)
                },
                vehicles = vehicleDtos,
                insights,
                recommendedActions = actions,
                evidence,
                followUps,
                warnings,
                note
            };

            return JsonSerializer.Serialize(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static object Kpi(
        string key,
        string label,
        int value,
        string unit,
        string icon,
        string tone,
        double? sharePercent = null)
        => new
        {
            key,
            label,
            value,
            unit,
            icon,
            tone,
            sharePercent,
            deltaLabel = sharePercent is > 0
                ? $"{sharePercent.Value.ToString("0.#", CultureInfo.InvariantCulture)}% of fleet"
                : null
        };

    private static object Slice(string key, string label, int count)
        => new { key, label, count };

    private static bool TryReadSummary(JsonElement root, out Summary summary)
    {
        summary = default;
        if (!root.TryGetProperty("summary", out var s) || s.ValueKind != JsonValueKind.Object)
            return false;

        var total = ReadInt(s, "total");
        if (total is null)
            return false;

        if (!s.TryGetProperty("online", out _)
            && !s.TryGetProperty("offline", out _)
            && !s.TryGetProperty("moving", out _))
            return false;

        summary = new Summary(
            total.Value,
            ReadInt(s, "online") ?? 0,
            ReadInt(s, "offline") ?? 0,
            ReadInt(s, "moving") ?? 0,
            ReadInt(s, "idle") ?? 0,
            ReadInt(s, "parked") ?? 0,
            ReadInt(s, "sos") ?? 0,
            ReadInt(s, "neverSeen") ?? 0,
            ReadInt(s, "unknown") ?? 0);
        return true;
    }

    private static List<VehicleRow> ReadVehicles(JsonElement root)
    {
        var list = new List<VehicleRow>();
        if (!root.TryGetProperty("sample", out var sample) || sample.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var row in sample.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;
            var vehicleId = ReadInt(row, "VehicleId") ?? ReadInt(row, "vehicleId");
            if (vehicleId is null)
                continue;

            var bucket = ReadString(row, "GpsBucket") ?? ReadString(row, "gpsBucket") ?? "unknown";
            list.Add(new VehicleRow(
                vehicleId.Value,
                ReadString(row, "VehicleName") ?? ReadString(row, "vehicleName") ?? $"Vehicle #{vehicleId}",
                ReadString(row, "RegistrationNumber") ?? ReadString(row, "registrationNumber") ?? "—",
                bucket,
                HumanBucket(bucket),
                BucketTone(bucket),
                ReadString(row, "VehicleStatusName") ?? ReadString(row, "vehicleStatusName"),
                ReadDouble(row, "Speed") ?? ReadDouble(row, "speed"),
                ReadString(row, "LastUpdate") ?? ReadString(row, "lastUpdate"),
                ReadBool(row, "Ignition") ?? ReadBool(row, "ignition")));
        }

        return list;
    }

    private static string BuildInterpretation(Summary s, double onlinePct, double offlinePct)
    {
        if (s.Total == 0)
            return "No fleet vehicles are available to interpret.";
        if (s.Sos > 0)
            return $"{s.Sos} vehicle(s) report SOS — treat as urgent before reviewing offline units.";
        if (offlinePct >= 50)
            return $"More than half the fleet looks offline ({offlinePct}%). Check GPS devices and last-update freshness.";
        if (onlinePct >= 80 && s.Sos == 0)
            return $"Fleet connectivity looks healthy ({onlinePct}% online) with no SOS events in this sample.";
        return $"Connectivity is mixed ({onlinePct}% online, {offlinePct}% offline). Review offline and never-seen units next.";
    }

    private static string[] BuildInsights(Summary s, double onlinePct, List<VehicleRow> vehicles)
    {
        var tips = new List<string>();
        if (s.Total == 0)
        {
            tips.Add("No vehicles in scope — confirm fleet inventory and tenant filters.");
            return tips.ToArray();
        }

        var offlinePct = s.Total > 0 ? Math.Round(s.Offline * 1000.0 / s.Total) / 10.0 : 0;
        if (offlinePct > 0)
            tips.Add($"{offlinePct}% of vehicles are offline");
        if (s.Parked > 0)
            tips.Add($"{s.Parked} vehicle{(s.Parked == 1 ? " is" : "s are")} parked");
        if (s.Sos == 0 && offlinePct < 40)
            tips.Add("No critical issues");
        if (s.Sos > 0)
            tips.Add($"{s.Sos} SOS alarm(s) need immediate attention");
        if (s.NeverSeen > 0)
            tips.Add($"{s.NeverSeen} vehicle(s) never seen on GPS");
        if (tips.Count == 0)
            tips.Add($"{s.Online} online / {s.Offline} offline ({onlinePct}% online).");

        // vehicles kept for future offline-sample insights (real sample only)
        _ = vehicles;

        return tips.Take(4).ToArray();
    }

    private static string[] BuildActions(Summary s)
    {
        var actions = new List<string>();
        if (s.Offline > 0)
            actions.Add("View Offline Vehicles");
        else
            actions.Add("View Online Vehicles");
        actions.Add("Show on Live Map");
        actions.Add("Generate Status Report");
        actions.Add("Refresh Data");
        if (s.Sos > 0)
            actions.Insert(0, "Open SOS Alerts");
        return actions.Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToArray();
    }

    private static string[] BuildFollowUps(Summary s)
    {
        var list = new List<string>
        {
            "Show all offline vehicles",
            "When did each vehicle last report?",
            "Which vehicles are parked right now?",
            "How healthy is my fleet today?"
        };
        if (s.Sos > 0)
            list.Insert(0, "Any SOS events this month?");
        return list.Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToArray();
    }

    private static object[] BuildEvidence(
        string? note,
        int offlineStale,
        double movingThreshold,
        List<VehicleRow> vehicles,
        string[] warnings)
    {
        var items = new List<object>
        {
            new { label = "Tool", detail = "get_fleet_status (MCP, read-only)" },
            new { label = "Source", detail = note ?? "VehicleCurrentLocation + GpsDevices" },
            new
            {
                label = "Rules",
                detail = $"Offline stale > {offlineStale} min; moving threshold {movingThreshold.ToString("0.##", CultureInfo.InvariantCulture)} km/h"
            },
            new { label = "Sample size", detail = $"{vehicles.Count} vehicle row(s) in response sample" }
        };
        foreach (var w in warnings.Take(3))
            items.Add(new { label = "Warning", detail = w });
        return items.ToArray();
    }

    private static string HumanBucket(string bucket) => bucket.ToLowerInvariant() switch
    {
        "moving" => "Moving",
        "idle" => "Idle",
        "parked" => "Parked",
        "sos" => "SOS",
        "offline" => "Offline",
        "never_seen" => "Never seen",
        _ => "Unknown"
    };

    private static string BucketTone(string bucket) => bucket.ToLowerInvariant() switch
    {
        "moving" => "ok",
        "idle" => "info",
        "parked" => "neutral",
        "sos" => "danger",
        "offline" => "warn",
        "never_seen" => "warn",
        _ => "neutral"
    };

    private static int? ReadInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i))
            return i;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var s))
            return s;
        return null;
    }

    private static double? ReadDouble(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d))
            return d;
        if (p.ValueKind == JsonValueKind.String
            && double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
            return s;
        return null;
    }

    private static bool? ReadBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string? ReadString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
            return null;
        var s = p.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static string[] ReadStringArray(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        return p.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }
}
