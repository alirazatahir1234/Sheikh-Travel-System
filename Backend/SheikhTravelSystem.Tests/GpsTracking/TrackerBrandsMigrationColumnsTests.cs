using System.Text.RegularExpressions;
using FluentAssertions;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>
/// Gt06Ev26rCommandTemplatesMigration filtered on <c>TrackerBrands.BrandKey</c>, a column no migration creates, so a
/// fresh database stopped there and every later migration never ran. Every column a migration reads or writes on
/// TrackerBrands must be created by a migration (the CREATE TABLE or an ADD).
/// </summary>
public class TrackerBrandsMigrationColumnsTests
{
    [Fact]
    public void Every_TrackerBrands_column_used_by_a_migration_is_created_by_a_migration()
    {
        var dir = Path.Combine(FindBackendRoot(), "SheikhTravelSystem.Infrastructure", "Persistence", "Migrations");
        var sources = Directory.EnumerateFiles(dir, "*.cs").ToDictionary(Path.GetFileName, File.ReadAllText);
        var all = string.Join("\n", sources.Values);

        var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var table = Regex.Match(all, @"CREATE TABLE (?:dbo\.)?TrackerBrands\s*\((.*?)\n\s*\);", RegexOptions.Singleline);
        table.Success.Should().BeTrue("TrackerCatalogMigration creates the table");
        foreach (Match c in Regex.Matches(table.Groups[1].Value, @"^\s*\[?(\w+)\]?\s+(INT|NVARCHAR|BIT|DATETIME2|DECIMAL|VARCHAR|UNIQUEIDENTIFIER)\b", RegexOptions.Multiline))
            created.Add(c.Groups[1].Value);
        foreach (Match c in Regex.Matches(all, @"ALTER TABLE (?:dbo\.)?TrackerBrands ADD \[?(\w+)\]?", RegexOptions.IgnoreCase))
            created.Add(c.Groups[1].Value);
        foreach (Match c in Regex.Matches(all, @"\(\s*connection\s*,\s*""TrackerBrands""\s*,\s*""(\w+)""", RegexOptions.IgnoreCase))
            created.Add(c.Groups[1].Value);

        var used = new List<(string File, string Column)>();
        foreach (var (file, text) in sources)
        {
            foreach (Match m in Regex.Matches(text, @"FROM (?:dbo\.)?TrackerBrands\s+WHERE\s+(.*?)(?:;|""""""|\)\s*;|\n\s*\n)", RegexOptions.Singleline | RegexOptions.IgnoreCase))
                foreach (Match col in Regex.Matches(m.Groups[1].Value, @"\b(\w+)\s*(?:=|LIKE|IS)\s", RegexOptions.IgnoreCase))
                    used.Add((file!, col.Groups[1].Value));
            foreach (Match m in Regex.Matches(text, @"UPDATE (?:dbo\.)?TrackerBrands SET (.*?)\bWHERE\b", RegexOptions.Singleline | RegexOptions.IgnoreCase))
                foreach (Match col in Regex.Matches(m.Groups[1].Value, @"\b(\w+)\s*=", RegexOptions.IgnoreCase))
                    used.Add((file!, col.Groups[1].Value));
            foreach (Match m in Regex.Matches(text, @"INSERT INTO (?:dbo\.)?TrackerBrands\s*\(([^)]*)\)", RegexOptions.IgnoreCase))
                foreach (var col in m.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    used.Add((file!, col.Trim('[', ']')));
        }

        used.Should().NotBeEmpty();
        var missing = used.Where(u => !created.Contains(u.Column) && !u.Column.StartsWith('@')).Distinct().ToList();
        missing.Should().BeEmpty(string.Join("\n", missing.Select(m => $"{m.File}: TrackerBrands.{m.Column} is never created")));
    }

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var backend = Path.Combine(dir.FullName, "Backend");
            if (Directory.Exists(Path.Combine(backend, "SheikhTravelSystem.Infrastructure"))) return backend;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate Backend/ root.");
    }
}
