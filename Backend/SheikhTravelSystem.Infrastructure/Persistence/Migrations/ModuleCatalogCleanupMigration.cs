using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Feature-governed alert types (geofence / speed) live only under GPS Add-ons (TenantFeatures):
/// prunes their orphaned TenantModules rows, hides their Modules rows, and aligns display names
/// with <see cref="ModuleRegistrySeed"/>.
/// </summary>
public static class ModuleCatalogCleanupMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (!await TableExistsAsync(connection, "Modules", cancellationToken))
            return;

        var featureCodes = AlertModuleCatalog.FeatureGovernedTypes
            .Select(t => t.ModuleCode)
            .Append(TenantModuleCatalog.RoadSpeedAlertsModuleCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var removed = 0;
        if (await TableExistsAsync(connection, "TenantModules", cancellationToken))
        {
            removed = await connection.ExecuteAsync(new CommandDefinition("""
                DELETE tm
                FROM TenantModules tm
                INNER JOIN Modules m ON m.Id = tm.ModuleId
                WHERE m.ModuleCode IN @Codes
                """, new { Codes = featureCodes }, cancellationToken: cancellationToken));
        }

        var hidden = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Modules SET Visible = 0 WHERE ModuleCode IN @Codes",
            new { Codes = featureCodes }, cancellationToken: cancellationToken));

        foreach (var entry in ModuleRegistrySeed.All.Where(e => e.Code is "GPS" or "ANALYTICS"))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Modules SET ModuleName = @Name, DisplayName = @DisplayName WHERE ModuleCode = @Code",
                new { entry.Code, entry.Name, entry.DisplayName }, cancellationToken: cancellationToken));
        }

        logger.LogInformation(
            "ModuleCatalogCleanupMigration applied. TenantModulesRemoved={Removed}, ModulesHidden={Hidden}.",
            removed, hidden);
    }

    private static async Task<bool> TableExistsAsync(
        IDbConnection connection, string table, CancellationToken cancellationToken)
    {
        var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT OBJECT_ID(@Name, 'U')",
            new { Name = table },
            cancellationToken: cancellationToken));
        return id is > 0;
    }
}
