using System.Text.RegularExpressions;
using FluentAssertions;
using SheikhTravelSystem.Infrastructure.Persistence.Migrations;

namespace SheikhTravelSystem.Tests.Notifications;

/// <summary>
/// NotificationTemplates.TenantId has been NOT NULL since NotificationTemplatesTenantEnforceMigration. A template
/// inserted without a tenant fails with SQL error 515 and stops every later migration at startup, so each
/// migration registered after the enforcement must name TenantId when it inserts a template.
/// </summary>
public class NotificationTemplateMigrationTenantTests
{
    private const string EnforceMigration = "NotificationTemplatesTenantEnforceMigration";

    // INSERT INTO [dbo.]NotificationTemplates ( column list )
    private static readonly Regex TemplateInsert = new(
        @"INSERT\s+INTO\s+(?:dbo\.)?NotificationTemplates\s*\((?<columns>[^)]*)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void Migrations_after_the_tenant_enforcement_insert_templates_with_a_TenantId()
    {
        var names = DatabaseMigrationRegistry.All.Select(m => m.Name).ToList();
        var enforcedAt = names.IndexOf(EnforceMigration);
        enforcedAt.Should().BeGreaterThanOrEqualTo(0, "the registry should still contain {0}", EnforceMigration);

        var folder = MigrationsFolder();
        var checkedInserts = 0;
        var offenders = new List<string>();

        foreach (var name in names.Skip(enforcedAt + 1))
        {
            var file = Path.Combine(folder, name + ".cs");
            File.Exists(file).Should().BeTrue("migration {0} should live in {0}.cs", name);

            foreach (var columns in InsertColumnLists(File.ReadAllText(file)))
            {
                checkedInserts++;
                if (!NamesTenantId(columns))
                    offenders.Add($"{name}: INSERT INTO NotificationTemplates ({Squash(columns)})");
            }
        }

        offenders.Should().BeEmpty("NotificationTemplates.TenantId is NOT NULL, so an insert without it fails the migration");
        checkedInserts.Should().BeGreaterThan(0, "the scan should find the template inserts it is meant to guard");
    }

    [Fact]
    public void Scanner_flags_the_insert_that_broke_startup()
    {
        // The statement TravelNexusRemindersMigration first shipped with.
        const string broken = """
            INSERT INTO NotificationTemplates (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, Variables, CreatedAt, IsDeleted)
            VALUES (N'travel_reminder', N'TravelNexus reminder', N'{{Title}}', N'{{Message}}', @Channel, 1, N'["Title","Message"]', GETUTCDATE(), 0);
            """;
        const string valid = """
            INSERT INTO NotificationTemplates
                (TenantId, TemplateKey, TemplateName, Subject, Body, Channel, IsActive, Language, Variables, CreatedAt, IsDeleted)
            SELECT TOP 1 ten.Id, @Key, @Name, @Subject, @Body, @Channel, 1, N'en', N'["title","message"]', GETUTCDATE(), 0
            FROM Tenants ten
            ORDER BY ten.Id;
            """;

        var brokenColumns = InsertColumnLists(broken).Should().ContainSingle().Subject;
        var validColumns = InsertColumnLists(valid).Should().ContainSingle().Subject;

        NamesTenantId(brokenColumns).Should().BeFalse();
        NamesTenantId(validColumns).Should().BeTrue();
    }

    private static IEnumerable<string> InsertColumnLists(string source)
        => TemplateInsert.Matches(source).Select(m => m.Groups["columns"].Value);

    private static bool NamesTenantId(string columns)
        => columns.Split(',').Any(c => c.Trim().Trim('[', ']').Equals("TenantId", StringComparison.OrdinalIgnoreCase));

    private static string Squash(string value) => Regex.Replace(value, @"\s+", " ").Trim();

    private static string MigrationsFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName, "Backend", "SheikhTravelSystem.Infrastructure", "Persistence", "Migrations");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the Infrastructure migrations folder.");
    }
}
