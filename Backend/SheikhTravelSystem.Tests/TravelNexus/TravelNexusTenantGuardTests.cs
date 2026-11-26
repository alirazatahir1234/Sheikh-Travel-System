using System.Text.RegularExpressions;
using FluentAssertions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Tests.Security.Sg01;

namespace SheikhTravelSystem.Tests.TravelNexus;

/// <summary>
/// Static guards for TravelNexus: every SQL touching a Travel* tenant table mentions TenantId,
/// and the module/permission registrations line up.
/// </summary>
public class TravelNexusTenantGuardTests
{
    [Fact]
    public void Sg01_guard_has_no_findings_in_TravelNexus_code()
    {
        var findings = Sg01TenantSqlGuardScanner.Scan(FindBackendRoot())
            .Where(f => f.RelativePath.Contains("TravelNexus", StringComparison.OrdinalIgnoreCase)
                        || f.Table.StartsWith("Travel", StringComparison.Ordinal))
            .ToList();

        findings.Should().BeEmpty(string.Join("\n", findings.Select(f => $"{f.RelativePath}:{f.Line} {f.MethodName} {f.Table}")));
    }

    [Fact]
    public void Sg01_guard_lists_TravelNexus_tenant_tables()
    {
        Sg01TenantSqlGuardScanner.TenantOwnedTables.Should().Contain(
            ["TravelSettings", "TravelVisaTypes", "TravelDocumentTypes", "TravelLeadSources", "TravelCustomerProfiles", "TravelTravelers"]);
    }

    [Fact]
    public void Every_TravelNexus_repository_statement_filters_by_tenant()
    {
        // Each SQL literal in the TravelNexus repositories that references a Travel* tenant table must
        // carry a TenantId predicate (TravelCountries and TravelAirports are platform-scope reference data).
        var dir = Path.Combine(FindBackendRoot(), "SheikhTravelSystem.Infrastructure", "Persistence", "Repositories", "TravelNexus");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
        {
            var text = File.ReadAllText(file);
            var parts = text.Split("\"\"\"");
            for (var i = 1; i < parts.Length; i += 2)
            {
                var sql = parts[i];
                // SELECT/JOIN fragments (no WHERE) are completed by a statement literal that carries the predicate.
                var isStatement = System.Text.RegularExpressions.Regex.IsMatch(sql, @"\b(WHERE|INSERT|UPDATE)\b");
                if (!isStatement) continue;
                var touchesTenantTable = System.Text.RegularExpressions.Regex.IsMatch(
                    sql, @"\b(FROM|JOIN|UPDATE|INTO)\s+(Travel(?!Countries\b|Airports\b)\w+|Customers|Users|Branches)\b");
                if (touchesTenantTable && !sql.Contains("TenantId", StringComparison.Ordinal))
                    offenders.Add($"{Path.GetFileName(file)}: {sql.Trim().Split('\n')[0]}");
            }
        }

        offenders.Should().BeEmpty(string.Join("\n", offenders));
    }

    [Fact]
    public void Travel_permissions_are_registered_and_mapped_to_TRAVEL_AGENCY_module()
    {
        TravelPermissions.All.Should().OnlyContain(p => p.StartsWith("Travel.", StringComparison.Ordinal));
        foreach (var code in TravelPermissions.All)
        {
            PermissionRegistrySeed.All.Should().Contain(e => e.PermissionCode == code);
            PermissionRegistrySeed.InferHierarchy(code).ModuleKey.Should().Be(TenantModuleCatalog.TravelAgencyModuleCode);
        }

        TenantModuleCatalog.All.Should().Contain(m => m.Code == TenantModuleCatalog.TravelAgencyModuleCode);
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain(TenantModuleCatalog.TravelAgencyModuleCode,
            "TravelNexus is opt-in per company");
        ModuleRegistrySeed.All.Should().Contain(e => e.Code == TenantModuleCatalog.TravelAgencyModuleCode && e.Status == "Active");
    }

    // ── Phase 8: the gaps the raw-literal scan above cannot see ─────────────

    private const string TenantTablePattern = @"\b(FROM|JOIN|UPDATE|INTO)\s+(Travel(?!Countries\b|Airports\b)\w+|Customers|Users|Branches)\b";

    private static IEnumerable<string> RepositoryFiles()
        => Directory.EnumerateFiles(Path.Combine(FindBackendRoot(), "SheikhTravelSystem.Infrastructure", "Persistence", "Repositories", "TravelNexus"), "*.cs");

    /// <summary>Text of the method declaration that encloses <paramref name="index"/> (header to the next header).</summary>
    private static string EnclosingMember(string text, int index)
    {
        var headers = Regex.Matches(text, @"^\s+(public|private|internal|protected)\s[^\n]*\(", RegexOptions.Multiline);
        var start = 0;
        var end = text.Length;
        foreach (Match h in headers)
        {
            if (h.Index <= index) start = h.Index;
            else { end = h.Index; break; }
        }
        return text[start..end];
    }

    /// <summary>The brace-delimited block that starts after <paramref name="from"/>.</summary>
    private static string BlockAfter(string text, int from)
    {
        var open = text.IndexOf('{', from);
        if (open < 0) return string.Empty;
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text[open..(i + 1)];
        }
        return text[open..];
    }

    [Fact]
    public void Interpolated_single_line_SQL_in_TravelNexus_repositories_filters_by_tenant()
    {
        // $"…" one-line statements (COUNT(*) totals, status roll-ups, state reads) are invisible to the raw-literal scan.
        // Each one touching a tenant table must carry TenantId itself or come from a method that builds the predicate.
        var offenders = new List<string>();
        foreach (var file in RepositoryFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"\$""(?!"")((?:[^""\\\n]|\\.)*)"""))
            {
                var sql = m.Groups[1].Value;
                if (!Regex.IsMatch(sql, @"\b(WHERE|INSERT|UPDATE)\b") || !Regex.IsMatch(sql, TenantTablePattern)) continue;
                if (sql.Contains("TenantId", StringComparison.Ordinal)) continue;
                if (sql.Contains('{') && EnclosingMember(text, m.Index).Contains("TenantId", StringComparison.Ordinal)) continue;
                offenders.Add($"{Path.GetFileName(file)}: {sql}");
            }
        }

        offenders.Should().BeEmpty(string.Join("\n", offenders));
    }

    [Fact]
    public void SQL_fragment_constants_that_join_tenant_tables_carry_the_tenant_predicate()
    {
        // {FromJoins}-style fragments are interpolated into many statements. A fragment that only names the root table leaves
        // the predicate to the statement (scanned above); one that JOINs a tenant table must repeat the tenant predicate in the
        // join itself, otherwise every statement using it would leak the joined rows across companies.
        var offenders = new List<string>();
        var checkedFragments = 0;
        foreach (var file in RepositoryFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text,
                @"(?:const|static readonly)\s+string\s+(\w+)\s*=\s*(?:\$?""""""(.*?)""""""|\$?""([^""\n]*)"")", RegexOptions.Singleline))
            {
                var sql = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
                var joins = Regex.Matches(sql, @"\bJOIN\s+(Travel(?!Countries\b|Airports\b)\w+|Customers|Users|Branches)\b\s+\w+\s+ON\s+([^\n]*)");
                if (joins.Count == 0) continue;
                checkedFragments++;
                foreach (Match j in joins)
                {
                    if (!j.Groups[2].Value.Contains("TenantId", StringComparison.Ordinal))
                        offenders.Add($"{Path.GetFileName(file)}: {m.Groups[1].Value} joins {j.Groups[1].Value} without a tenant predicate");
                }
            }
        }

        checkedFragments.Should().BeGreaterThan(5, "the list and finance fragments join several tenant tables");
        offenders.Should().BeEmpty(string.Join("\n", offenders));
    }

    [Fact]
    public void Every_repository_method_that_receives_a_branch_scope_applies_it()
    {
        // A branch-scoped read that ignores its scope would show a branch user company-wide rows (SG-01 inside a tenant).
        var offenders = new List<string>();
        var checked_ = 0;
        foreach (var file in RepositoryFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"(public|internal|private)\s+(?:static\s+)?(?:async\s+)?Task(?:<[^\n(]*>)?\s+(\w+)\(([^)]*)\)", RegexOptions.Singleline))
            {
                if (!m.Groups[3].Value.Contains("TravelBranchScope scope", StringComparison.Ordinal)) continue;
                checked_++;
                var body = BlockAfter(text, m.Index + m.Length);
                if (!Regex.IsMatch(body, @"\bscope\b"))
                    offenders.Add($"{Path.GetFileName(file)}: {m.Groups[2].Value}");
            }
        }

        checked_.Should().BeGreaterThan(40, "the branch-scoped repository surface is large");
        offenders.Should().BeEmpty(string.Join("\n", offenders));
    }

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var backend = Path.Combine(dir.FullName, "Backend");
            if (Directory.Exists(Path.Combine(backend, "SheikhTravelSystem.Infrastructure")))
                return backend;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate Backend/ root.");
    }
}
