using FluentAssertions;

namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 Stage 1 — tenant SQL guard. Scans source for tenant-owned SQL without TenantId.
/// Failures are suppressed only by <see cref="Sg01TenantSqlGuardAllowlist"/> (may only shrink)
/// or a <c>// PLATFORM-SCOPE</c> marker on the method.
/// </summary>
public class Sg01TenantSqlGuardTests
{
    [Fact]
    public void Tenant_owned_SQL_without_TenantId_must_be_allowlisted_or_platform_scope()
    {
        var backend = FindBackendRoot();
        var findings = Sg01TenantSqlGuardScanner.Scan(backend);

        findings.Should().BeEmpty(because: Format(findings));
    }

    [Fact]
    public void Allowlist_is_seeded_from_sweep_and_may_only_shrink()
    {
        // Guardrail: Stage 1 seed size. Do not grow this number — shrink as Stage 2/3 fix rows.
        const int stage1SeedCount = 77;
        Sg01TenantSqlGuardAllowlist.Entries.Should().HaveCount(stage1SeedCount,
            because: "allowlist may only shrink; raise this constant only if re-seeding from an updated sweep before Stage 2 starts");

        Sg01TenantSqlGuardAllowlist.Entries.Count(e => e.Classification == "LEAK")
            .Should().BeGreaterThan(0);
        Sg01TenantSqlGuardAllowlist.Entries.Count(e => e.Classification == "AUTHORIZATION BUG")
            .Should().Be(5);

        foreach (var e in Sg01TenantSqlGuardAllowlist.Entries)
        {
            (e.Classification == "LEAK" || e.Classification == "AUTHORIZATION BUG")
                .Should().BeTrue(because: $"{e.FileName}::{e.MethodName} must be LEAK or AUTHORIZATION BUG");
            e.FileName.Should().EndWith(".cs");
            e.MethodName.Should().NotBeNullOrWhiteSpace();
            e.SweepNote.Should().NotBeNullOrWhiteSpace();
        }
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

    private static string Format(IReadOnlyList<Sg01TenantSqlGuardScanner.Finding> findings)
    {
        if (findings.Count == 0) return "no findings";
        return "SG-01 tenant SQL guard violations (fix, allowlist-shrink after fix, or // PLATFORM-SCOPE):\n"
               + string.Join("\n", findings.Select(f =>
                   $"- {f.RelativePath}:{f.Line} {f.MethodName} :: {f.Table} :: {f.Snippet}"));
    }
}
