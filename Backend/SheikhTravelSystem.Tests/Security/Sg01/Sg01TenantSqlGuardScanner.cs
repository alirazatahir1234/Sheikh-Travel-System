using System.Text.RegularExpressions;

namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 Stage 1 source scanner: SQL touching a tenant-owned table in a method that
/// never mentions TenantId fails unless allowlisted or marked <c>// PLATFORM-SCOPE</c>.
/// </summary>
public static class Sg01TenantSqlGuardScanner
{
    /// <summary>
    /// Tables that carry TenantId (CLAUDE.md + TenantSchemaMigration / sweep).
    /// Child tables without TenantId are out of scope for this Stage 1 heuristic.
    /// </summary>
    public static readonly IReadOnlyList<string> TenantOwnedTables =
    [
        "Vehicles", "Drivers", "DriverDocuments", "ComplianceDocuments", "Trips",
        "Bookings", "Customers", "Invoices", "Payments", "Settlements", "FuelLogs",
        "MaintenanceOrders", "Maintenance", "Inspections", "Assignments", "AssignmentHistory",
        "Branches", "Departments", "Users", "GpsDevices", "Alerts", "Notifications",
        "RentalBookings", "RentalRates", "AllowanceRules", "DriverAllowanceRules",
        "Routes", "Geofences", "GpsAlertRules", "GpsAlertEvents", "GpsDeviceCommands",
        "GpsPositions", "GpsTrips", "AuditLogs", "PromoCodes",
        "WebsiteContactRequests", "WebsiteDemoRequests",
        // TravelNexus (TravelCountries is platform-scope reference data)
        "TravelSettings", "TravelVisaTypes", "TravelDocumentTypes", "TravelLeadSources",
        "TravelCustomerProfiles", "TravelTravelers",
        "TravelNumberSequences", "TravelLostReasons", "TravelEnquiries", "TravelEnquiryActivities",
        "TravelVisaCases", "TravelVisaApplicants", "TravelVisaStatusHistory",
        "TravelChecklistTemplates", "TravelChecklistItems", "TravelApplicantDocuments", "TravelDocumentFiles",
        "TravelHotels", "TravelPackages", "TravelPackageHotels", "TravelPackagePrices", "TravelGroups", "TravelGroupMembers",
        "TravelGroupHotels", "TravelGroupRooms", "TravelRoomAssignments", "TravelGroupTransport", "TravelGroupStatusHistory",
        "TravelSuppliers", "TravelBookings", "TravelFlightBookings", "TravelFlightSegments", "TravelFlightPassengers",
        "TravelHotelBookings", "TravelHotelBookingRooms", "TravelHotelBookingGuests", "TravelBookingCancellations", "TravelBookingHistory",
        "TravelInvoices", "TravelInvoiceLines", "TravelInvoiceInstallments", "TravelPayments", "TravelPaymentAllocations",
        "TravelCreditNotes", "TravelRefunds", "TravelFinanceHistory"
    ];

    private static readonly Regex SqlTableTouch = BuildTableTouchRegex();

    private static readonly Regex MethodSignature = new(
        @"^(?<indent>[ \t]*)(?:public|private|protected|internal)\s+(?:(?:static|async|virtual|override|sealed|partial|new)\s+)+(?<ret>[\w.<>,\[\]\s\?]+?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex PlatformScope = new(
        @"//\s*PLATFORM-SCOPE\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> SkipFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Sweep: auth email/token lookups are OK (PLATFORM-AUTH).
        "AuthRepository.cs",
        "AuthSessionService.cs",
        // Local/dev-only helpers — not a tenant API path.
        "DevDataService.cs",
        "DatabaseSeeder.cs"
    };

    public sealed record Finding(
        string RelativePath,
        string FileName,
        string MethodName,
        string Table,
        int Line,
        string Snippet);

    public static IReadOnlyList<Finding> Scan(string backendRoot)
    {
        var findings = new List<Finding>();
        var roots = new[]
        {
            Path.Combine(backendRoot, "SheikhTravelSystem.Infrastructure"),
            Path.Combine(backendRoot, "SheikhTravelSystem.Application")
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (!ShouldScan(path))
                    continue;

                ScanFile(backendRoot, path, findings);
            }
        }

        return findings;
    }

    private static bool ShouldScan(string path)
    {
        var n = path.Replace('\\', '/');
        if (n.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || n.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || n.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith("Dtos.cs", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith("Dto.cs", StringComparison.OrdinalIgnoreCase))
            return false;

        var fileName = Path.GetFileName(path);
        if (SkipFileNames.Contains(fileName))
            return false;

        // Sweep: hosted jobs that loop tenants / scan TenantId IS NOT NULL are OK (PLATFORM-JOB).
        if (fileName.EndsWith("HostedService.cs", StringComparison.OrdinalIgnoreCase)
            && !fileName.Equals("ComplianceReminderHostedService.cs", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static void ScanFile(string backendRoot, string path, List<Finding> findings)
    {
        var text = File.ReadAllText(path);
        var relative = Path.GetRelativePath(backendRoot, path).Replace('\\', '/');
        var fileName = Path.GetFileName(path);
        var lineStarts = BuildLineStarts(text);

        foreach (var method in EnumerateMethods(text))
        {
            if (method.Body.Contains("TenantId", StringComparison.Ordinal)
                || method.Body.Contains("tenantId", StringComparison.Ordinal))
                continue;

            var headerStart = Math.Max(0, method.SignatureIndex - 240);
            var header = text[headerStart..method.BodyStart];
            if (PlatformScope.IsMatch(header) || PlatformScope.IsMatch(method.Body))
                continue;

            if (Sg01TenantSqlGuardAllowlist.IsAllowed(fileName, method.Name))
                continue;

            foreach (Match m in SqlTableTouch.Matches(method.Body))
            {
                var absIndex = method.BodyStart + m.Index;
                findings.Add(new Finding(
                    relative,
                    fileName,
                    method.Name,
                    m.Groups["table"].Value,
                    LineNumberAt(lineStarts, absIndex),
                    Truncate(m.Value)));
            }
        }
    }

    private sealed record MethodSpan(string Name, int SignatureIndex, int BodyStart, string Body);

    private static IEnumerable<MethodSpan> EnumerateMethods(string text)
    {
        foreach (Match sig in MethodSignature.Matches(text))
        {
            var name = sig.Groups["name"].Value;
            if (name is "if" or "while" or "for" or "switch" or "catch" or "using" or "lock" or "where")
                continue;

            // Reject when "name" is actually a generic type fragment (e.g. Task from Task<...>).
            var ret = sig.Groups["ret"].Value.Trim();
            if (ret.Length == 0)
                continue;

            var paren = text.IndexOf('(', sig.Index);
            if (paren < 0)
                continue;

            var afterParams = SkipBalanced(text, paren, '(', ')');
            if (afterParams < 0)
                continue;

            var i = afterParams;
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;

            if (i < text.Length && text.AsSpan(i).StartsWith("where", StringComparison.Ordinal))
            {
                var brace = text.IndexOf('{', i);
                if (brace < 0)
                    continue;
                i = brace;
            }

            if (i >= text.Length || text[i] != '{')
                continue;

            var bodyEnd = SkipBalanced(text, i, '{', '}');
            if (bodyEnd < 0)
                continue;

            var bodyStart = i;
            var body = text[bodyStart..bodyEnd];
            yield return new MethodSpan(name, sig.Index, bodyStart, body);
        }
    }

    private static int SkipBalanced(string text, int openIndex, char open, char close)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            var c = text[i];
            if (c == open)
                depth++;
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                    return i + 1;
            }
        }
        return -1;
    }

    private static int[] BuildLineStarts(string text)
    {
        var list = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                list.Add(i + 1);
        }
        return list.ToArray();
    }

    private static int LineNumberAt(int[] lineStarts, int index)
    {
        var lo = 0;
        var hi = lineStarts.Length - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (lineStarts[mid] <= index)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return hi + 1;
    }

    private static Regex BuildTableTouchRegex()
    {
        // Case-sensitive PascalCase table names to avoid matching locals like `bookings`.
        var alternation = string.Join("|", TenantOwnedTables.Select(Regex.Escape));
        return new Regex(
            $@"(?:\bFROM|\bJOIN|\bUPDATE|\bINTO|\bDELETE\s+FROM)\s+(?:\[?dbo\]?\.)?\[?(?<table>{alternation})\]?\b",
            RegexOptions.Compiled);
    }

    private static string Truncate(string s)
    {
        var oneLine = Regex.Replace(s, @"\s+", " ").Trim();
        return oneLine.Length <= 120 ? oneLine : oneLine[..117] + "...";
    }
}
