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
