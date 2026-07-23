namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Stage 8 Permission Registry seed metadata for migration + API enrichment + soft policy gates.
/// </summary>
public static class PermissionRegistrySeed
{
    public sealed record Entry(
        string PermissionCode,
        string DisplayName,
        string Category,
        string Action,
        string? ModuleKey,
        string? FeatureKey,
        int SortOrder,
        bool Visible = true);

    private static readonly Lazy<IReadOnlyList<Entry>> LazyAll = new(BuildAll);
    private static readonly Lazy<Dictionary<string, Entry>> LazyByCode = new(() =>
        All.ToDictionary(e => e.PermissionCode, StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<Entry> All => LazyAll.Value;

    public static Entry? Find(string permissionCode)
        => LazyByCode.Value.TryGetValue(permissionCode, out var e) ? e : null;

    public static string DeriveAction(string permissionCode)
    {
        var tail = permissionCode.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        return tail.ToUpperInvariant() switch
        {
            "VIEW" or "ALERTVIEW" or "COMMANDVIEW" or "REPORTVIEW" or "VIEWPERFORMANCE" => "View",
            "CREATE" or "REQUESTCREATE" => "Create",
            "EDIT" or "UPDATE" or "ALERTACKNOWLEDGE" or "ALERTRESOLVE" => "Edit",
            "DELETE" or "ALERTDELETE" or "ALERTARCHIVE" => "Delete",
            "MANAGE" or "MANAGESTATUS" or "WORKORDERMANAGE" or "WORKSHOPMANAGE"
                or "VENDORMANAGE" or "REQUESTAPPROVE" or "ASSIGN" or "EXECUTEWRITE"
                or "COMMANDSEND" or "COMMANDENGINECUTOFF" or "COMMANDPOSITIONREQUEST"
                or "COMMANDRESTART" or "COMMANDRELAY" or "COMMANDBUZZER" or "COMMANDCUSTOMSMS"
                or "COMMANDRETRY" or "COMMANDCANCEL" or "RESET" => "Manage",
            _ => "Other"
        };
    }

    public static string DeriveDisplayName(string permissionCode)
    {
        var parts = permissionCode.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return permissionCode;
        return string.Join(' ', parts.Select(Humanize));
    }

    private static IReadOnlyList<Entry> BuildAll()
    {
        var codes = PlatformPermissions.All
            .Concat(FleetPermissions.All)
            .Concat(DriverPermissions.All)
            .Concat(MaintenancePermissions.All)
            .Concat(GpsPermissions.All)
            .Concat(OperationsPermissions.All)
            .Concat(FinancePermissions.All)
            .Concat(AnalyticsPermissions.All)
            .Concat(AiPermissions.All)
            .Concat(NotificationPermissions.All)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = new List<Entry>(codes.Count);
        var sort = 10;
        foreach (var code in codes)
        {
            var (category, moduleKey, featureKey) = MapPolicy(code);
            list.Add(new Entry(
                code,
                DeriveDisplayName(code),
                category,
                DeriveAction(code),
                moduleKey,
                featureKey,
                sort,
                true));
            sort += 10;
        }

        return list;
    }

    /// <summary>
    /// Soft module/feature mapping. Null ModuleKey/FeatureKey = no soft gate (permission always passes).
    /// </summary>
    private static (string Category, string? ModuleKey, string? FeatureKey) MapPolicy(string code)
    {
        if (code.StartsWith("Platform.", StringComparison.OrdinalIgnoreCase))
        {
            // Platform admin surface is gated by ACCESS when mapped; system ops stay ungated.
            if (code.Contains(".Migrations.", StringComparison.OrdinalIgnoreCase)
                || code.Contains(".System.", StringComparison.OrdinalIgnoreCase))
                return ("Platform", null, null);
            return ("Platform", "ACCESS", null);
        }

        if (code.StartsWith("Vehicle.", StringComparison.OrdinalIgnoreCase))
            return ("Fleet", "FLEET", "vehicles");
        if (code.StartsWith("Driver.", StringComparison.OrdinalIgnoreCase))
            return ("Fleet", "FLEET", "drivers");
        if (code.StartsWith("Maintenance.", StringComparison.OrdinalIgnoreCase))
            return ("Fleet", "FLEET", "maintenance");
        if (code.StartsWith("Gps.", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("GPS.", StringComparison.OrdinalIgnoreCase))
            return ("Fleet", "GPS", "gps-tracking");
        if (code.StartsWith("Booking.", StringComparison.OrdinalIgnoreCase))
            return ("Operations", "TRAVEL", "bookings");
        if (code.StartsWith("Trip.", StringComparison.OrdinalIgnoreCase))
            return ("Operations", "TRAVEL", "trips");
        if (code.StartsWith("Route.", StringComparison.OrdinalIgnoreCase))
            return ("Operations", "TRAVEL", null);
        if (code.StartsWith("Fuel.", StringComparison.OrdinalIgnoreCase))
            return ("Finance", "FLEET", "fuel-logs");
        if (code.StartsWith("Payment.", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("Invoice.", StringComparison.OrdinalIgnoreCase))
            return ("Finance", "FINANCE", null);
        if (code.StartsWith("Customer.", StringComparison.OrdinalIgnoreCase))
            return ("CRM", "CRM", null);
        if (code.StartsWith("Report.", StringComparison.OrdinalIgnoreCase))
            return ("Analytics", "ANALYTICS", null);
        if (code.StartsWith("Ai.", StringComparison.OrdinalIgnoreCase))
            return ("AI", "AI", null);
        if (code.StartsWith("Notification.", StringComparison.OrdinalIgnoreCase))
            return ("Platform", "NOTIFICATIONS", null);

        return ("Other", null, null);
    }

    private static string Humanize(string segment)
    {
        if (string.IsNullOrEmpty(segment)) return segment;
        // Insert spaces before capitals in PascalCase-ish tokens already split by '.'
        var chars = new List<char> { char.ToUpperInvariant(segment[0]) };
        for (var i = 1; i < segment.Length; i++)
        {
            var c = segment[i];
            if (char.IsUpper(c) && char.IsLower(segment[i - 1]))
                chars.Add(' ');
            chars.Add(c);
        }
        return new string(chars.ToArray());
    }
}
