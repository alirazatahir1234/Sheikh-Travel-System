using System.Text.RegularExpressions;
using FluentAssertions;

namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 Pass 2, step 1 — the compliance/assignment joins must match the joined entity's tenant,
/// not only its Id. Without this, a document whose EntityId points at another tenant's driver or
/// vehicle surfaces that tenant's name. The generic guard cannot see join predicates, so these
/// tests pin them on the SQL text.
/// </summary>
public class Sg01ComplianceJoinIsolationTests
{
    private static readonly string Backend = FindBackendRoot();

    [Fact]
    public void Compliance_documents_query_scopes_joined_vehicles_and_drivers_to_the_document_tenant()
    {
        var sql = MethodBody("FleetRepository.cs", "GetComplianceDocumentsAsync");

        sql.Should().Contain("c.TenantId = @TenantId");
        Normalize(sql).Should().Contain("LEFT JOIN Vehicles v ON c.EntityType = 'Vehicle' AND c.EntityId = v.Id AND v.TenantId = c.TenantId");
        Normalize(sql).Should().Contain("LEFT JOIN Drivers d ON c.EntityType = 'Driver' AND c.EntityId = d.Id AND d.TenantId = c.TenantId");
    }

    [Fact]
    public void Assignments_query_scopes_joined_driver_to_the_vehicle_tenant()
    {
        var sql = Normalize(MethodBody("FleetRepository.cs", "GetAssignmentsAsync"));

        sql.Should().Contain("v.TenantId = @TenantId");
        sql.Should().Contain("LEFT JOIN Drivers d ON a.DriverId = d.Id AND d.TenantId = v.TenantId");
    }

    [Fact]
    public void Compliance_reminder_scan_keeps_driver_vehicle_and_documents_in_one_tenant()
    {
        var sql = Normalize(MethodBody("ComplianceReminderHostedService.cs", "ScanAsync"));

        sql.Should().Contain("INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.TenantId = d.TenantId");
        sql.Should().Contain("c.EntityId = v.Id AND c.TenantId = v.TenantId");
    }

    private static string MethodBody(string fileName, string methodName)
    {
        var path = Directory.EnumerateFiles(Backend, fileName, SearchOption.AllDirectories)
            .First(p => !p.Contains("/obj/") && !p.Contains("/bin/"));
        var text = File.ReadAllText(path);

        var declaration = Regex.Match(text, $@"\bTask\b[^\n;=]*\s{Regex.Escape(methodName)}\s*\(");
        declaration.Success.Should().BeTrue(because: $"{fileName} must still define {methodName}");
        var start = declaration.Index;

        var open = text.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0)
                return text[open..(i + 1)];
        }
        throw new InvalidOperationException($"Unbalanced braces in {fileName}::{methodName}");
    }

    private static string Normalize(string s) => Regex.Replace(s, @"\s+", " ");

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
