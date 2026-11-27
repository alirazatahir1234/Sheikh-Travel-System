using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Routing;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.API.Controllers;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Tests.Security.Sg01;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>
/// Tenant isolation for the geofences workspace, pinned the way the SG-01 and TravelNexus guards pin Dapper code:
/// every SQL statement that touches a tenant-owned table carries a TenantId predicate, so another tenant's ids read
/// as not found; the SG-01 scanner has nothing to report; and every write route needs Gps.GeofenceManage.
/// </summary>
public class GeofenceWorkspaceTenantGuardTests
{
    private static readonly string Backend = FindBackendRoot();

    private static readonly string[] GeofenceFiles =
    [
        "SheikhTravelSystem.Infrastructure/Persistence/Repositories/GeofenceWorkspaceRepository.cs",
        "SheikhTravelSystem.Infrastructure/Persistence/Repositories/GeofenceWorkspaceRepository.Writes.cs",
        "SheikhTravelSystem.Infrastructure/Persistence/Repositories/GpsTrackingRepository.GeofenceRules.cs",
        "SheikhTravelSystem.Infrastructure/Services/Gps/GeofenceAlertNotifier.cs",
        "SheikhTravelSystem.Infrastructure/Services/GeofenceRuleMonitorHostedService.cs",
    ];

    /// <summary>Tables whose rows belong to a tenant (directly or, for assignments, through their geofence).</summary>
    private static readonly Regex TenantTableTouch = new(
        @"\b(FROM|JOIN|UPDATE|INTO)\s+(dbo\.)?(Geofences|GeofenceGroups|GeofenceTypes|GeofenceRecipients|GeofenceVisits|GeofenceNotArrivedLog|Vehicles|Drivers|Users|Branches|Departments|GpsDevices|GpsDeviceCommands|GpsAlertRules)\b",
        RegexOptions.Compiled);

    [Fact]
    public void Every_statement_touching_a_tenant_table_filters_by_tenant()
    {
        var offenders = new List<string>();
        foreach (var relative in GeofenceFiles)
        {
            var text = File.ReadAllText(Path.Combine(Backend, relative));
            var parts = text.Split("\"\"\"");
            for (var i = 1; i < parts.Length; i += 2)
            {
                var sql = parts[i];
                if (!Regex.IsMatch(sql, @"\b(SELECT|INSERT|UPDATE|DELETE)\b"))
                    continue; // column-list fragments completed by another literal
                if (TenantTableTouch.IsMatch(sql) && !sql.Contains("TenantId", StringComparison.Ordinal))
                    offenders.Add($"{Path.GetFileName(relative)}: {sql.Trim().Split('\n')[0]}");
            }
        }

        offenders.Should().BeEmpty(string.Join("\n", offenders));
    }

    [Fact]
    public void Sg01_guard_has_no_findings_in_the_geofence_workspace()
    {
        var names = GeofenceFiles.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var findings = Sg01TenantSqlGuardScanner.Scan(Backend)
            .Where(f => names.Contains(f.FileName) || f.Table.StartsWith("Geofence", StringComparison.Ordinal))
            .ToList();

        findings.Should().BeEmpty(string.Join("\n", findings.Select(f => $"{f.RelativePath}:{f.Line} {f.MethodName} {f.Table}")));
    }

    [Fact]
    public void Sg01_guard_lists_the_new_geofence_tables()
    {
        Sg01TenantSqlGuardScanner.TenantOwnedTables.Should().Contain(
            ["GeofenceGroups", "GeofenceTypes", "GeofenceRecipients", "GeofenceVisits", "GeofenceNotArrivedLog"]);
    }

    [Fact]
    public void Repository_takes_the_tenant_from_the_tenant_context_only()
    {
        var text = string.Join("\n", GeofenceFiles.Take(2).Select(f => File.ReadAllText(Path.Combine(Backend, f))));

        text.Should().Contain("tenantContext.GetRequiredTenantId()");
        Regex.IsMatch(text, @"public async Task[^\(]*\([^\)]*\btenantId\b", RegexOptions.IgnoreCase)
            .Should().BeFalse("no public repository method accepts a tenant id from the caller");
    }

    [Fact]
    public void Workspace_routes_are_gated_by_the_geofencing_add_on_and_writes_need_geofence_manage()
    {
        var methods = typeof(GpsTrackingController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any(a =>
                a.Template is { } t && t.StartsWith("geofences/", StringComparison.Ordinal)
                && (t.Contains("workspace") || t.Contains("summary") || t.Contains("visits") || t.Contains("groups")
                    || t.Contains("types") || t.Contains("inside") || t.Contains("activity") || t.Contains("test-shape")
                    || t.Contains("bulk") || t.Contains("import") || t.Contains("recipient-options"))))
            .ToList();

        methods.Should().HaveCountGreaterThan(15);
        foreach (var method in methods)
        {
            method.GetCustomAttribute<RequireFeatureAttribute>().Should().NotBeNull(method.Name);

            var verb = method.GetCustomAttributes<HttpMethodAttribute>().SelectMany(a => a.HttpMethods).First();
            var isWrite = verb is "POST" or "PUT" or "DELETE";
            var needsManage = method.GetCustomAttributes<RequirePermissionAttribute>()
                .Any(a => a.Policy == GpsPermissions.GeofenceManage);
            if (isWrite || method.Name.Contains("RecipientOptions"))
                needsManage.Should().BeTrue($"{method.Name} changes data or lists people");
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
}
