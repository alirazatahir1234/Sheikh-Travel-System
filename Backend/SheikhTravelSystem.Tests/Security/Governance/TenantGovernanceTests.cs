using FluentAssertions;

namespace SheikhTravelSystem.Tests.Security.Governance;

public class TenantGovernanceTests
{
    [Fact]
    public void Catalog_matches_TenantSchemaMigration_table_list()
    {
        var infra = FindInfrastructureRoot();
        var migration = Directory.EnumerateFiles(infra, "TenantSchemaMigration.cs", SearchOption.AllDirectories).Single();
        var text = File.ReadAllText(migration);

        foreach (var table in TenantScopedTableCatalog.TablesWithTenantIdColumn)
            text.Should().Contain($"\"{table}\"", because: "governance catalog must stay aligned with TenantSchemaMigration");
    }

    [Fact]
    public void Strict_repositories_enforce_tenant_context()
    {
        var findings = TenantSqlGovernanceScanner.ScanStrictRepositoryContext(FindInfrastructureRoot());
        findings.Should().BeEmpty(because: Format(findings));
    }

    [Fact]
    public void Runtime_INSERT_into_tenant_tables_must_include_TenantId()
    {
        var findings = TenantSqlGovernanceScanner.Scan(FindInfrastructureRoot())
            .Where(f => f.Operation == "Insert")
            .ToList();
        findings.Should().BeEmpty(because: Format(findings));
    }

    [Fact]
    public void VehicleTracking_access_must_use_Vehicles_tenant_boundary()
    {
        var findings = TenantSqlGovernanceScanner.Scan(FindInfrastructureRoot())
            .Where(f => f.Operation == "ParentJoin")
            .ToList();
        findings.Should().BeEmpty(because: Format(findings));
    }

    [Fact]
    public void Phase3B_core_repositories_must_not_have_unscoped_SQL()
    {
        var findings = TenantSqlGovernanceScanner.Scan(FindInfrastructureRoot())
            .Where(f => f.Operation == "SelectUpdateDelete")
            .ToList();
        findings.Should().BeEmpty(because: Format(findings));
    }

    [Fact]
    public void PublicLead_remains_PublicByDesign_tenant_one()
    {
        var infra = FindInfrastructureRoot();
        var path = Directory.EnumerateFiles(infra, "PublicLeadRepository.cs", SearchOption.AllDirectories).Single();
        var text = File.ReadAllText(path);
        text.Should().Contain("(1, @FirstName");
        text.Should().Contain("(1, @Name");
        text.Should().NotContain("GetRequiredTenantId");
    }

    [Fact]
    public void AuditLog_keeps_tenant_filter()
    {
        var infra = FindInfrastructureRoot();
        var path = Directory.EnumerateFiles(infra, "AuditLogRepository.cs", SearchOption.AllDirectories).Single();
        File.ReadAllText(path).Should().Contain("a.TenantId = @TenantId");
    }

    [Fact]
    public void Allowlist_entries_are_documented()
    {
        TenantGovernanceAllowlist.Entries.Should().NotBeEmpty();
        foreach (var e in TenantGovernanceAllowlist.Entries)
        {
            e.Reason.Should().NotBeNullOrWhiteSpace();
            e.RelativePathGlob.Should().NotBeNullOrWhiteSpace();
            e.Operation.Should().NotBeNullOrWhiteSpace();
        }
    }

    private static string FindInfrastructureRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var infra = Path.Combine(dir.FullName, "Backend", "SheikhTravelSystem.Infrastructure");
            if (Directory.Exists(infra))
                return infra;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate Infrastructure project root.");
    }

    private static string Format(IReadOnlyList<TenantSqlGovernanceScanner.Finding> findings)
    {
        if (findings.Count == 0) return "no findings";
        return "Governance violations:\n" + string.Join("\n", findings.Select(f =>
            $"- [{f.Operation}] {f.RelativePath} :: {f.Table} :: {f.Snippet}"));
    }
}
