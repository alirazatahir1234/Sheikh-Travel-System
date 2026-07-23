using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 2 Company Business Model: thin Feature Registry + Companies menu label.
/// Persistence remains Tenants; FeatureDefinitions/TenantFeatures are metadata only.
/// </summary>
public static class CompanyFeatureRegistryMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FeatureDefinitions')
            CREATE TABLE FeatureDefinitions (
                FeatureKey NVARCHAR(100) NOT NULL,
                ModuleKey NVARCHAR(50) NOT NULL,
                Name NVARCHAR(200) NOT NULL,
                Description NVARCHAR(500) NULL,
                SortOrder INT NOT NULL CONSTRAINT DF_FeatureDefinitions_SortOrder DEFAULT (0),
                IsActive BIT NOT NULL CONSTRAINT DF_FeatureDefinitions_IsActive DEFAULT (1),
                CONSTRAINT PK_FeatureDefinitions PRIMARY KEY (FeatureKey)
            );

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantFeatures')
            CREATE TABLE TenantFeatures (
                TenantId INT NOT NULL,
                FeatureKey NVARCHAR(100) NOT NULL,
                IsEnabled BIT NOT NULL CONSTRAINT DF_TenantFeatures_IsEnabled DEFAULT (1),
                CONSTRAINT PK_TenantFeatures PRIMARY KEY (TenantId, FeatureKey),
                CONSTRAINT FK_TenantFeatures_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT FK_TenantFeatures_FeatureDefinitions FOREIGN KEY (FeatureKey) REFERENCES FeatureDefinitions(FeatureKey)
            );

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_TenantFeatures_FeatureKey' AND object_id = OBJECT_ID('TenantFeatures'))
                CREATE INDEX IX_TenantFeatures_FeatureKey ON TenantFeatures (FeatureKey);
            """, cancellationToken: cancellationToken));

        var catalog = BuildCatalog();
        foreach (var feature in catalog)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM FeatureDefinitions WHERE FeatureKey = @FeatureKey)
                    INSERT INTO FeatureDefinitions (FeatureKey, ModuleKey, Name, Description, SortOrder, IsActive)
                    VALUES (@FeatureKey, @ModuleKey, @Name, @Description, @SortOrder, 1);
                ELSE
                    UPDATE FeatureDefinitions
                    SET ModuleKey = @ModuleKey, Name = @Name, Description = @Description,
                        SortOrder = @SortOrder, IsActive = 1
                    WHERE FeatureKey = @FeatureKey;
                """, feature, cancellationToken: cancellationToken));
        }

        // Enable features for tenants that already have the parent module.
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantFeatures (TenantId, FeatureKey, IsEnabled)
            SELECT tm.TenantId, fd.FeatureKey, 1
            FROM TenantModules tm
            INNER JOIN Modules m ON m.Id = tm.ModuleId
            INNER JOIN FeatureDefinitions fd ON fd.ModuleKey = m.ModuleCode AND fd.IsActive = 1
            WHERE NOT EXISTS (
                SELECT 1 FROM TenantFeatures tf
                WHERE tf.TenantId = tm.TenantId AND tf.FeatureKey = fd.FeatureKey);
            """, cancellationToken: cancellationToken));

        // Menu label: Tenants → Companies (route stays /platform/tenants).
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm
            SET pm.Name = N'Companies'
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId
            WHERE pm.Route = N'/platform/tenants'
              AND (pm.Name = N'Tenants' OR pm.Name = N'Companies');
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "CompanyFeatureRegistryMigration applied ({FeatureCount} feature definitions + Companies menu label).",
            catalog.Count);
    }

    private static List<object> BuildCatalog()
    {
        var sort = 0;
        var rows = new List<object>();

        void Add(string featureKey, string moduleKey, string name, string description)
        {
            rows.Add(new
            {
                FeatureKey = featureKey,
                ModuleKey = moduleKey,
                Name = name,
                Description = description,
                SortOrder = ++sort
            });
        }

        // Catalog keyed to Modules.ModuleCode; FeatureKey uses product/legacy keys where useful.
        Add("dashboard", "DASHBOARD", "Dashboard", "Operational home dashboards");
        Add("vehicles", "FLEET", "Vehicles", "Vehicle registry and fleet assets");
        Add("drivers", "FLEET", "Drivers", "Driver profiles and assignments");
        Add("fuel-logs", "FLEET", "Fuel Logs", "Fuel receipt and consumption tracking");
        Add("maintenance", "FLEET", "Maintenance", "Service and maintenance workflows");
        Add("gps-tracking", "GPS", "GPS Tracking", "Live tracking and GPS telemetry");
        Add("rental", "RENTAL", "Vehicle Rental", "Rental bookings and fleet hire");
        Add("bookings", "TRAVEL", "Bookings", "Travel agency bookings");
        Add("routes", "TRAVEL", "Routes", "Route planning and catalog");
        Add("trips", "TRAVEL", "Trips", "Trip lifecycle and dispatch");
        Add("customers", "CRM", "Customers", "CRM customer directory");
        Add("payments", "FINANCE", "Payments", "Payments and collections");
        Add("hr", "HR", "HR", "Human resources module features");
        Add("reports", "ANALYTICS", "Reports", "Analytics and operational reports");
        Add("audit-logs", "ANALYTICS", "Audit Logs", "Platform audit trail viewing");
        Add("users", "ACCESS", "Users", "User directory within access control");
        Add("driver-allowance-rules", "ACCESS", "Driver Allowance Rules", "Allowance rule configuration");

        return rows;
    }
}
