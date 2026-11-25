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
            .Concat(SmartOpsPermissions.All)
            .Concat(RentalPermissions.All)
            .Concat(TravelPermissions.All)
            .Concat(WebsitePermissions.All)
            .Concat(WhatsAppPermissions.All)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = new List<Entry>(codes.Count);
        var sort = 10;
        foreach (var code in codes)
        {
            var (category, moduleKey, featureKey) = InferHierarchy(code);
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
    /// Module-governed permissions use <c>ModuleKey</c> (<c>TenantModules</c>) and keep <c>FeatureKey = null</c>
    /// so opt-in <c>TenantFeatures</c> rows (e.g. "rental", "bookings") never strip unrelated module claims.
    /// </summary>
    public static (string Category, string? ModuleKey, string? FeatureKey) InferHierarchy(string code)
        => MapPolicy(code);

    private static (string Category, string? ModuleKey, string? FeatureKey) MapPolicy(string code)
    {
        if (code.StartsWith("Platform.", StringComparison.OrdinalIgnoreCase))
        {
            // Platform admin surface is gated by ACCESS when mapped; system ops & core shell stay ungated.
            if (code.Contains(".Migrations.", StringComparison.OrdinalIgnoreCase)
                || code.Contains(".System.", StringComparison.OrdinalIgnoreCase)
                || code.StartsWith("Platform.Dashboard.", StringComparison.OrdinalIgnoreCase)
                || code.StartsWith("Platform.Branches.", StringComparison.OrdinalIgnoreCase))
                return ("Platform", null, null);
            return ("Platform", "ACCESS", null);
        }

        if (code.StartsWith("Vehicle.", StringComparison.OrdinalIgnoreCase))
            return ("Fleet", "FLEET", null);
        if (code.StartsWith("Driver.", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("DriverWallet.", StringComparison.OrdinalIgnoreCase))
            return ("Fleet", "FLEET", null);
        if (code.StartsWith("Maintenance.", StringComparison.OrdinalIgnoreCase))
            return ("Fleet", "FLEET", null);
        if (code.StartsWith("Gps.", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("GPS.", StringComparison.OrdinalIgnoreCase))
            return ("Fleet", "GPS", null);
        if (code.StartsWith("Booking.", StringComparison.OrdinalIgnoreCase))
            return ("Operations", "BOOKINGS", null);
        if (code.StartsWith("Trip.", StringComparison.OrdinalIgnoreCase))
            return ("Operations", "TRAVEL", null);
        if (code.StartsWith("Route.", StringComparison.OrdinalIgnoreCase))
            return ("Operations", "TRAVEL", null);
        if (code.StartsWith("Fuel.", StringComparison.OrdinalIgnoreCase))
            return ("Finance", "FLEET", null);
        if (code.StartsWith("Rental.", StringComparison.OrdinalIgnoreCase))
            return ("Fleet", "RENTAL", null);
        if (code.StartsWith("Travel.", StringComparison.OrdinalIgnoreCase))
            return ("Travel Agency", TenantModuleCatalog.TravelAgencyModuleCode, null);
        if (code.StartsWith("Payment.", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("Invoice.", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("Settlement.", StringComparison.OrdinalIgnoreCase))
            return ("Finance", "FINANCE", null);
        if (code.StartsWith("Customer.", StringComparison.OrdinalIgnoreCase))
            return ("CRM", "CRM", null);
        if (code.StartsWith("Report.", StringComparison.OrdinalIgnoreCase))
            return ("Analytics", "ANALYTICS", null);
        if (code.StartsWith("Ai.", StringComparison.OrdinalIgnoreCase))
            return ("AI", "AI", null);
        // Notifications are core UX for every tenant. NOTIFICATIONS is Beta / not in
        // TenantModuleCatalog enableable set — do not soft-gate behind a missing module.
        if (code.StartsWith("Notification.", StringComparison.OrdinalIgnoreCase))
            return ("Platform", null, null);
        if (code.StartsWith("SmartOps.", StringComparison.OrdinalIgnoreCase))
            return ("Operations", "TRAVEL", null);
        if (code.StartsWith("Website.", StringComparison.OrdinalIgnoreCase))
            return ("Platform", null, null);
        if (code.StartsWith("WhatsApp.", StringComparison.OrdinalIgnoreCase))
            return ("CRM", null, null);

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
