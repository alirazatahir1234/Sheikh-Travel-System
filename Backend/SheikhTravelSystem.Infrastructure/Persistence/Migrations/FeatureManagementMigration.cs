using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 5 Feature Management: extend FeatureDefinitions metadata + TenantFeatures audit columns.
/// No runtime flags, targeting, or Feature Builder.
/// </summary>
public static class FeatureManagementMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddColumnIfMissingAsync(connection, "FeatureDefinitions", "DisplayName", "NVARCHAR(200) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "FeatureDefinitions", "Category", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "FeatureDefinitions", "Icon", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "FeatureDefinitions", "Route", "NVARCHAR(200) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "FeatureDefinitions", "Visible", "BIT NOT NULL CONSTRAINT DF_FeatureDefinitions_Visible DEFAULT (1)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "FeatureDefinitions", "Status", "NVARCHAR(50) NOT NULL CONSTRAINT DF_FeatureDefinitions_Status DEFAULT N'Active'", cancellationToken);
        await AddColumnIfMissingAsync(connection, "FeatureDefinitions", "IsMobileSupported", "BIT NOT NULL CONSTRAINT DF_FeatureDefinitions_IsMobileSupported DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "FeatureDefinitions", "IsAISupported", "BIT NOT NULL CONSTRAINT DF_FeatureDefinitions_IsAISupported DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "FeatureDefinitions", "IsGPSSupported", "BIT NOT NULL CONSTRAINT DF_FeatureDefinitions_IsGPSSupported DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "FeatureDefinitions", "DocumentationUrl", "NVARCHAR(500) NULL", cancellationToken);

        await AddColumnIfMissingAsync(connection, "TenantFeatures", "EnabledBy", "INT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "TenantFeatures", "EnabledDate", "DATETIME2 NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "TenantFeatures", "LastModified", "DATETIME2 NULL", cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE FeatureDefinitions
            SET DisplayName = Name
            WHERE DisplayName IS NULL OR LTRIM(RTRIM(DisplayName)) = '';
            """, cancellationToken: cancellationToken));

        foreach (var row in FeatureRegistrySeed.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM FeatureDefinitions WHERE FeatureKey = @FeatureKey)
                    UPDATE FeatureDefinitions SET
                        ModuleKey = @ModuleKey,
                        Name = @Name,
                        DisplayName = @DisplayName,
                        Description = @Description,
                        Category = @Category,
                        Icon = @Icon,
                        Route = @Route,
                        SortOrder = @SortOrder,
                        Visible = @Visible,
                        Status = @Status,
                        IsMobileSupported = @IsMobileSupported,
                        IsAISupported = @IsAISupported,
                        IsGPSSupported = @IsGPSSupported,
                        DocumentationUrl = @DocumentationUrl,
                        IsActive = CASE WHEN @Status IN (N'Deprecated', N'Disabled') THEN 0 ELSE 1 END
                    WHERE FeatureKey = @FeatureKey;
                ELSE
                    INSERT INTO FeatureDefinitions (
                        FeatureKey, ModuleKey, Name, Description, SortOrder, IsActive,
                        DisplayName, Category, Icon, Route, Visible, Status,
                        IsMobileSupported, IsAISupported, IsGPSSupported, DocumentationUrl)
                    VALUES (
                        @FeatureKey, @ModuleKey, @Name, @Description, @SortOrder,
                        CASE WHEN @Status IN (N'Deprecated', N'Disabled') THEN 0 ELSE 1 END,
                        @DisplayName, @Category, @Icon, @Route, @Visible, @Status,
                        @IsMobileSupported, @IsAISupported, @IsGPSSupported, @DocumentationUrl);
                """,
                new
                {
                    row.FeatureKey,
                    row.ModuleKey,
                    row.Name,
                    row.DisplayName,
                    row.Description,
                    row.Category,
                    row.Icon,
                    row.Route,
                    row.SortOrder,
                    row.Visible,
                    row.Status,
                    row.IsMobileSupported,
                    row.IsAISupported,
                    row.IsGPSSupported,
                    row.DocumentationUrl
                },
                cancellationToken: cancellationToken));
        }

        // Ensure tenant rows for features under installed modules (do not overwrite existing IsEnabled).
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantFeatures (TenantId, FeatureKey, IsEnabled, EnabledDate, LastModified)
            SELECT tm.TenantId, fd.FeatureKey, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM TenantModules tm
            INNER JOIN Modules m ON m.Id = tm.ModuleId
            INNER JOIN FeatureDefinitions fd ON fd.ModuleKey = m.ModuleCode AND fd.Visible = 1
            WHERE fd.Status IN (N'Active', N'Beta')
              AND NOT EXISTS (
                SELECT 1 FROM TenantFeatures tf
                WHERE tf.TenantId = tm.TenantId AND tf.FeatureKey = fd.FeatureKey);

            UPDATE TenantFeatures
            SET LastModified = COALESCE(LastModified, SYSUTCDATETIME()),
                EnabledDate = CASE WHEN IsEnabled = 1 THEN COALESCE(EnabledDate, SYSUTCDATETIME()) ELSE EnabledDate END
            WHERE LastModified IS NULL OR (IsEnabled = 1 AND EnabledDate IS NULL);
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "FeatureManagementMigration applied ({FeatureCount} feature definitions + TenantFeatures audit columns).",
            FeatureRegistrySeed.All.Count);
    }

    private static async Task AddColumnIfMissingAsync(
        IDbConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition($"""
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @Table AND COLUMN_NAME = @Column)
                ALTER TABLE [{table}] ADD [{column}] {definition};
            """,
            new { Table = table, Column = column },
            cancellationToken: cancellationToken));
    }
}
