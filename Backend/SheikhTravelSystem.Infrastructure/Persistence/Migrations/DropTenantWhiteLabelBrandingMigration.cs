using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Removes unused white-label branding columns (LogoUrl, PrimaryColor) from Tenants and TenantBranding.
/// SheikhGo Nexus is single-product; per-tenant logo/theme white-labeling is not offered.
///
/// Local DB findings before drop (2026-11-01 query):
/// - Tenants.LogoUrl / TenantBranding.LogoUrl: all NULL (0 non-null rows)
/// - Tenants.PrimaryColor / TenantBranding.PrimaryColor: only default seed "#007A57" on tenant Id=1;
///   other tenants NULL — not custom white-label data
/// - PlatformSettings Branding keys LogoUrl/PrimaryColor/FaviconUrl/LoginBackgroundUrl/SidebarColor: none
/// Contact/locale columns (Website, SupportEmail, Country, CurrencyCode, TimeZone) are kept.
/// </summary>
public static class DropTenantWhiteLabelBrandingMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await DropColumnIfExistsAsync(connection, "TenantBranding", "LogoUrl", cancellationToken);
        await DropColumnIfExistsAsync(connection, "TenantBranding", "PrimaryColor", cancellationToken);
        await DropColumnIfExistsAsync(connection, "Tenants", "LogoUrl", cancellationToken);
        await DropColumnIfExistsAsync(connection, "Tenants", "PrimaryColor", cancellationToken);

        // Orphaned UI-only white-label keys that were never mapped to TenantBranding columns.
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM PlatformSettings
            WHERE Category = N'Branding'
              AND [Key] IN (N'LogoUrl', N'PrimaryColor', N'FaviconUrl', N'LoginBackgroundUrl', N'SidebarColor');
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "DropTenantWhiteLabelBrandingMigration applied (LogoUrl/PrimaryColor dropped; contact/locale kept).");
    }

    private static async Task DropColumnIfExistsAsync(
        IDbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition($"""
            IF EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'{table}' AND COLUMN_NAME = N'{column}')
                ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
            """, cancellationToken: cancellationToken));
    }
}
