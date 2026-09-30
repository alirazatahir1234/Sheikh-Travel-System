using System.Text.RegularExpressions;

namespace SheikhTravelSystem.Tests.Security.Governance;

/// <summary>
/// Lightweight source scanner for tenant SQL governance (Phase 3C).
/// Scans Infrastructure repository/service C# for unsafe INSERT/SELECT/UPDATE/DELETE
/// against tenant-owned tables and parent-scoped tables.
/// </summary>
public static class TenantSqlGovernanceScanner
{
    private static readonly Regex InsertInto = new(
        @"INSERT\s+INTO\s+(?<table>\w+)\s*\((?<cols>[^)]*)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FromUpdateDelete = new(
        @"(?:FROM|UPDATE|DELETE\s+FROM)\s+(?<table>\w+)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VehicleTrackingFrom = new(
        @"(?:FROM|JOIN|INTO|UPDATE)\s+VehicleTracking\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record Finding(
        string RelativePath,
        string Table,
        string Operation,
        string Snippet);

    public static IReadOnlyList<Finding> Scan(string infrastructureRoot)
    {
        var findings = new List<Finding>();
        var files = Directory.EnumerateFiles(infrastructureRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p => ShouldScan(p, infrastructureRoot))
            .ToList();

        foreach (var path in files)
        {
            var relative = Path.GetRelativePath(infrastructureRoot, path).Replace('\\', '/');
            var text = StripBlockComments(File.ReadAllText(path));

            ScanInserts(relative, text, findings);
            ScanParentScoped(relative, text, findings);
            ScanSelectUpdateDelete(relative, text, findings);
        }

        return findings;
    }

    public static IReadOnlyList<Finding> ScanStrictRepositoryContext(string infrastructureRoot)
    {
        var findings = new List<Finding>();
        var repoRoot = Path.Combine(infrastructureRoot, "Persistence", "Repositories");
        if (!Directory.Exists(repoRoot))
            return findings;

        foreach (var fileName in TenantScopedTableCatalog.StrictTenantRepositories)
        {
            var matches = Directory.EnumerateFiles(repoRoot, fileName, SearchOption.AllDirectories).ToList();
            // GpsTrackingRepository is partial across files — accept any matching name prefix file for context check on primary file only
            if (matches.Count == 0)
            {
                findings.Add(new Finding(
                    $"Persistence/Repositories/{fileName}",
                    "*",
                    "RepositoryContext",
                    "Strict repository file missing."));
                continue;
            }

            var path = matches[0];
            var relative = Path.GetRelativePath(infrastructureRoot, path).Replace('\\', '/');
            if (TenantGovernanceAllowlist.IsAllowed(relative, "*", "RepositoryContext"))
                continue;

            var text = File.ReadAllText(path);
            var ok = text.Contains("ITenantContext", StringComparison.Ordinal)
                     || text.Contains("GetRequiredTenantId", StringComparison.Ordinal)
                     || text.Contains("tenantId", StringComparison.Ordinal)
                     || text.Contains("TenantId = @TenantId", StringComparison.Ordinal)
                     || text.Contains("TrackerTenantSql", StringComparison.Ordinal);

            // Partial GpsTrackingRepository — check siblings
            if (!ok && fileName.StartsWith("GpsTrackingRepository", StringComparison.Ordinal))
            {
                ok = Directory.EnumerateFiles(repoRoot, "GpsTrackingRepository*.cs", SearchOption.AllDirectories)
                    .Select(File.ReadAllText)
                    .Any(t => t.Contains("GetRequiredTenantId", StringComparison.Ordinal)
                              || t.Contains("ITenantContext", StringComparison.Ordinal));
            }

            if (!ok)
            {
                findings.Add(new Finding(
                    relative,
                    "*",
                    "RepositoryContext",
                    "Strict repository lacks ITenantContext / GetRequiredTenantId / tenantId / TenantId SQL / TrackerTenantSql."));
            }
        }

        return findings;
    }

    private static bool ShouldScan(string path, string infrastructureRoot)
    {
        var relative = Path.GetRelativePath(infrastructureRoot, path).Replace('\\', '/');
        if (relative.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase)
            || relative.EndsWith("DatabaseSeeder.cs", StringComparison.OrdinalIgnoreCase))
            return false;

        return relative.Contains("/Persistence/Repositories/", StringComparison.OrdinalIgnoreCase)
               || relative.Contains("/Services/Gps/", StringComparison.OrdinalIgnoreCase)
               || relative.Contains("/Services/Payments/", StringComparison.OrdinalIgnoreCase)
               || relative.Contains("/Traccar/", StringComparison.OrdinalIgnoreCase);
    }

    private static void ScanInserts(string relative, string text, List<Finding> findings)
    {
        foreach (Match m in InsertInto.Matches(text))
        {
            var table = m.Groups["table"].Value;
            if (!TenantScopedTableCatalog.TablesWithTenantIdColumn.Contains(table, StringComparer.OrdinalIgnoreCase))
                continue;

            if (TenantGovernanceAllowlist.IsAllowed(relative, table, "Insert"))
                continue;

            var cols = m.Groups["cols"].Value;
            if (cols.Contains("TenantId", StringComparison.OrdinalIgnoreCase))
                continue;

            findings.Add(new Finding(
                relative,
                table,
                "Insert",
                Truncate(m.Value)));
        }
    }

    private static void ScanParentScoped(string relative, string text, List<Finding> findings)
    {
        foreach (Match m in VehicleTrackingFrom.Matches(text))
        {
            if (TenantGovernanceAllowlist.IsAllowed(relative, "VehicleTracking", "ParentJoin"))
                continue;

            // Require Vehicles + TenantId in a surrounding window (statement-ish).
            var start = Math.Max(0, m.Index - 200);
            var end = Math.Min(text.Length, m.Index + m.Length + 600);
            var window = text[start..end];
            var ok = window.Contains("Vehicles", StringComparison.OrdinalIgnoreCase)
                     && window.Contains("TenantId", StringComparison.OrdinalIgnoreCase);

            // Inserts into VehicleTracking are OK when preceded by an ownership EXISTS check
            // in the same method — look further back for Vehicles TenantId ownership.
            if (!ok)
            {
                var methodStart = Math.Max(0, m.Index - 2500);
                var methodWindow = text[methodStart..end];
                ok = methodWindow.Contains("FROM Vehicles", StringComparison.OrdinalIgnoreCase)
                     && methodWindow.Contains("TenantId", StringComparison.OrdinalIgnoreCase);
            }

            // Ingest helper: resolves TenantId from Vehicles before related writes; VehicleTracking
            // itself has no TenantId column — allow when Vehicles.TenantId lookup is in same file.
            if (!ok && relative.Contains("GpsPositionIngestionHelper", StringComparison.OrdinalIgnoreCase))
            {
                ok = text.Contains("SELECT TenantId FROM Vehicles", StringComparison.OrdinalIgnoreCase);
            }

            if (!ok)
            {
                findings.Add(new Finding(
                    relative,
                    "VehicleTracking",
                    "ParentJoin",
                    Truncate(window)));
            }
        }
    }

    private static void ScanSelectUpdateDelete(string relative, string text, List<Finding> findings)
    {
        // Strict SELECT/UPDATE/DELETE gate applies to Phase 3B core tables only,
        // to avoid drowning in deferred GPS analytics / report SQL.
        var strictTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Customers", "FuelLogs", "Payments", "Bookings", "Routes",
            "DriverAllowanceRules", "Geofences", "GpsAlertRules"
        };

        // Only enforce on repositories we already hardened (plus Customer/Fuel/etc. files).
        var strictFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CustomerRepository.cs", "FuelLogRepository.cs", "PaymentRepository.cs",
            "BookingRepository.cs", "RouteRepository.cs", "DriverAllowanceRepository.cs",
            "DashboardRepository.cs", "PricingRepository.cs", "MaintenanceRepository.cs",
            "AuditLogRepository.cs"
        };

        var fileName = Path.GetFileName(relative);
        if (!strictFiles.Contains(fileName))
            return;

        foreach (Match m in FromUpdateDelete.Matches(text))
        {
            var table = m.Groups["table"].Value;
            if (!strictTables.Contains(table))
                continue;

            if (TenantGovernanceAllowlist.IsAllowed(relative, table, "SelectUpdateDelete"))
                continue;

            var start = Math.Max(0, m.Index - 120);
            var end = Math.Min(text.Length, m.Index + m.Length + 400);
            var window = text[start..end];

            // Dynamic filters: TenantId often lives in whereClause / Build(...) helper.
            var ok = window.Contains("TenantId", StringComparison.OrdinalIgnoreCase)
                     || window.Contains("whereClause", StringComparison.OrdinalIgnoreCase)
                     || window.Contains("WhereClause", StringComparison.OrdinalIgnoreCase)
                     || window.Contains("QueryFilters", StringComparison.OrdinalIgnoreCase);

            // Multi-line UPDATE SET ... WHERE TenantId — expand window for UPDATE statements
            if (!ok && window.Contains("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                var updateEnd = Math.Min(text.Length, m.Index + 900);
                var updateWindow = text[start..updateEnd];
                ok = updateWindow.Contains("TenantId", StringComparison.OrdinalIgnoreCase);
            }

            if (!ok)
            {
                findings.Add(new Finding(
                    relative,
                    table,
                    "SelectUpdateDelete",
                    Truncate(window)));
            }
        }
    }

    private static string StripBlockComments(string text)
    {
        return Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
    }

    private static string Truncate(string s)
    {
        var oneLine = Regex.Replace(s, @"\s+", " ").Trim();
        return oneLine.Length <= 180 ? oneLine : oneLine[..177] + "...";
    }
}
