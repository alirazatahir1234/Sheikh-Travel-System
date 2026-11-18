using System.Text.RegularExpressions;
using FluentAssertions;

namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 Pass 2, step 3 — AI fleet-intelligence / prediction SQL. These methods take a tenantId,
/// so the generic guard is satisfied, but several statements read Maintenance and GpsAlertEvents
/// across every tenant. GpsAlertEvents has no TenantId and Maintenance.TenantId is nullable, so
/// both must be anchored through a tenant-filtered Vehicles join. Pinned on the SQL text.
/// </summary>
public class Sg01FleetIntelligenceIsolationTests
{
    private static readonly string Backend = FindBackendRoot();

    [Theory]
    [InlineData("FleetIntelligenceServices.cs", "ComputeAsync")]
    [InlineData("FleetIntelligenceServices.cs", "GenerateMorningDigestAsync")]
    [InlineData("FleetIntelligenceServices.cs", "RefreshAsync")]
    [InlineData("PredictionCopilotManagement.cs", "CaptureFeaturesAsync")]
    [InlineData("PredictionCopilotManagement.cs", "RunHeuristicPredictionsAsync")]
    [InlineData("PredictionCopilotManagement.cs", "GetDatasetStatusAsync")]
    public void Every_Maintenance_and_GpsAlertEvents_statement_is_anchored_to_the_tenant(string file, string method)
    {
        foreach (var statement in SqlStatements(MethodBody(file, method)))
        {
            var touchesUnscopedTable =
                Regex.IsMatch(statement, @"\bFROM\s+(Maintenance|GpsAlertEvents)\b", RegexOptions.IgnoreCase);
            if (!touchesUnscopedTable)
                continue;

            var isCorrelatedSubquery = Regex.IsMatch(statement, @"m\.VehicleId\s*=\s*v\.Id") &&
                                       Normalize(statement).Contains("v.TenantId = @TenantId");
            if (isCorrelatedSubquery)
                continue;

            Normalize(statement).Should().MatchRegex(
                @"INNER JOIN Vehicles v ON v\.Id = \w+\.VehicleId AND v\.TenantId = @TenantId",
                because: $"{file}::{method} reads Maintenance/GpsAlertEvents and must scope through Vehicles:\n{statement}");
        }
    }

    [Fact]
    public void Driver_risk_prediction_only_counts_the_tenant_drivers()
    {
        var sql = Normalize(MethodBody("PredictionCopilotManagement.cs", "RunHeuristicPredictionsAsync"));
        sql.Should().Contain("INNER JOIN Drivers d ON d.Id = a.DriverId AND d.TenantId = @TenantId");
    }

    /// <summary>Raw/verbatim string literals inside the method body — one per SQL statement.</summary>
    private static IEnumerable<string> SqlStatements(string body)
    {
        foreach (Match m in Regex.Matches(body, "\"\"\"(.*?)\"\"\"", RegexOptions.Singleline))
            yield return m.Groups[1].Value;
        var withoutRaw = Regex.Replace(body, "\"\"\".*?\"\"\"", "", RegexOptions.Singleline);
        foreach (Match m in Regex.Matches(withoutRaw, "@?\"((?:[^\"\\\\]|\\\\.|\"\")*)\""))
            yield return m.Groups[1].Value;
    }

    private static string MethodBody(string fileName, string methodName)
    {
        var path = Directory.EnumerateFiles(Backend, fileName, SearchOption.AllDirectories)
            .First(p => !p.Contains("/obj/") && !p.Contains("/bin/"));
        var text = File.ReadAllText(path);

        var declaration = Regex.Match(text, $@"\bTask\b[^\n;=]*\s{Regex.Escape(methodName)}\s*\(");
        declaration.Success.Should().BeTrue(because: $"{fileName} must still define {methodName}");

        var open = text.IndexOf('{', declaration.Index);
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
