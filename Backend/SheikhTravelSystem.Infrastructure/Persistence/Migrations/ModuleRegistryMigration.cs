using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Text.Json;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 3 Module Registry: metadata columns on Modules + Active/ComingSoon catalog seed.
/// Does not create a parallel registry table or Module CRUD.
/// </summary>
public static class ModuleRegistryMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddColumnIfMissingAsync(connection, "Modules", "DisplayName", "NVARCHAR(200) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "Description", "NVARCHAR(500) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "Category", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "Version", "NVARCHAR(50) NOT NULL CONSTRAINT DF_Modules_Version DEFAULT N'1.0.0'", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "Icon", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "Route", "NVARCHAR(200) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "SortOrder", "INT NOT NULL CONSTRAINT DF_Modules_SortOrder DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "DependenciesJson", "NVARCHAR(MAX) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "Visible", "BIT NOT NULL CONSTRAINT DF_Modules_Visible DEFAULT (1)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "IsMobileSupported", "BIT NOT NULL CONSTRAINT DF_Modules_IsMobileSupported DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "IsAISupported", "BIT NOT NULL CONSTRAINT DF_Modules_IsAISupported DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "IsGPSSupported", "BIT NOT NULL CONSTRAINT DF_Modules_IsGPSSupported DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "Status", "NVARCHAR(50) NOT NULL CONSTRAINT DF_Modules_Status DEFAULT N'Active'", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Modules", "DocumentationUrl", "NVARCHAR(500) NULL", cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Modules
            SET DisplayName = ModuleName
            WHERE DisplayName IS NULL OR LTRIM(RTRIM(DisplayName)) = '';
            """, cancellationToken: cancellationToken));

        foreach (var row in ModuleRegistrySeed.All)
        {
            var depsJson = row.Dependencies.Length == 0
                ? null
                : JsonSerializer.Serialize(row.Dependencies);
            var legacyJson = row.LegacyKeys.Length == 0
                ? null
                : JsonSerializer.Serialize(row.LegacyKeys);

            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM Modules WHERE ModuleCode = @Code)
                    UPDATE Modules SET
                        ModuleName = @Name,
                        DisplayName = @DisplayName,
                        Description = @Description,
                        Category = @Category,
                        Version = @Version,
                        Icon = @Icon,
                        Route = @Route,
                        SortOrder = @SortOrder,
                        DependenciesJson = @DependenciesJson,
                        Visible = @Visible,
                        IsMobileSupported = @IsMobileSupported,
                        IsAISupported = @IsAISupported,
                        IsGPSSupported = @IsGPSSupported,
                        Status = @Status,
                        DocumentationUrl = @DocumentationUrl,
                        LegacyKeysJson = COALESCE(@LegacyKeysJson, LegacyKeysJson)
                    WHERE ModuleCode = @Code;
                ELSE
                    INSERT INTO Modules (
                        ModuleCode, ModuleName, LegacyKeysJson, DisplayName, Description, Category,
                        Version, Icon, Route, SortOrder, DependenciesJson, Visible,
                        IsMobileSupported, IsAISupported, IsGPSSupported, Status, DocumentationUrl)
                    VALUES (
                        @Code, @Name, @LegacyKeysJson, @DisplayName, @Description, @Category,
                        @Version, @Icon, @Route, @SortOrder, @DependenciesJson, @Visible,
                        @IsMobileSupported, @IsAISupported, @IsGPSSupported, @Status, @DocumentationUrl);
                """,
                new
                {
                    row.Code,
                    row.Name,
                    DisplayName = row.DisplayName,
                    row.Description,
                    row.Category,
                    row.Version,
                    row.Icon,
                    row.Route,
                    row.SortOrder,
                    DependenciesJson = depsJson,
                    row.Visible,
                    row.IsMobileSupported,
                    row.IsAISupported,
                    row.IsGPSSupported,
                    row.Status,
                    row.DocumentationUrl,
                    LegacyKeysJson = legacyJson
                },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation(
            "ModuleRegistryMigration applied ({Count} module registry rows).",
            ModuleRegistrySeed.All.Count);
    }

    private static async Task AddColumnIfMissingAsync(
        System.Data.IDbConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition($"""
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @Table AND COLUMN_NAME = @Column)
                ALTER TABLE [{table}] ADD [{column}] {definition};
            """,
            new { Table = table, Column = column },
            cancellationToken: ct));
    }
}
