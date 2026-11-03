using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds Permissions.FeatureKey for subscription / soft-gate entitlement checks.
/// Local DBs that ran PermissionEngineFoundationMigration before FeatureKey existed need this.
/// </summary>
public static class PermissionsFeatureKeyMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = N'Permissions' AND COLUMN_NAME = N'FeatureKey')
                ALTER TABLE [Permissions] ADD [FeatureKey] NVARCHAR(100) NULL;
            """, cancellationToken: cancellationToken));

        // Keep SQL in Infrastructure (ADR-003); seed metadata stays in Application.
        foreach (var row in PermissionRegistrySeed.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Permissions SET
                    DisplayName = @DisplayName,
                    Category = @Category,
                    SortOrder = @SortOrder,
                    Visible = @Visible,
                    Action = @Action,
                    ModuleKey = @ModuleKey,
                    FeatureKey = @FeatureKey
                WHERE PermissionCode = @PermissionCode;
                """,
                new
                {
                    row.PermissionCode,
                    row.DisplayName,
                    row.Category,
                    row.SortOrder,
                    Visible = row.Visible,
                    row.Action,
                    row.ModuleKey,
                    row.FeatureKey
                },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation(
            "PermissionsFeatureKeyMigration applied (FeatureKey column + seed sync on Permissions).");
    }
}
