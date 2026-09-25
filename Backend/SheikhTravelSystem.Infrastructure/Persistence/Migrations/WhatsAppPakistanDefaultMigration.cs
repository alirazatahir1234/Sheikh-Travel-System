using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// SheikhGo uses a single Pakistan WhatsApp line — make PK the default and deactivate UAE.
/// Tokens stay in env (WhatsApp__Pakistan__*); UAE rows remain for history but are inactive.
/// </summary>
public static class WhatsAppPakistanDefaultMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'WhatsAppAccounts', N'U') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: cancellationToken)) != 1)
        {
            logger.LogInformation("WhatsAppPakistanDefaultMigration skipped — WhatsAppAccounts missing.");
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            -- Pakistan becomes the sole default / active sender.
            UPDATE WhatsAppAccounts
            SET IsDefault = 1,
                IsActive = 1,
                Status = N'Active',
                UpdatedAt = SYSUTCDATETIME()
            WHERE Code = N'PK';

            UPDATE WhatsAppAccounts
            SET IsDefault = 0,
                IsActive = 0,
                Status = N'Inactive',
                UpdatedAt = SYSUTCDATETIME()
            WHERE Code = N'UAE';

            -- Any other codes: clear default if PK exists.
            IF EXISTS (SELECT 1 FROM WhatsAppAccounts WHERE Code = N'PK')
            BEGIN
                UPDATE WhatsAppAccounts
                SET IsDefault = 0, UpdatedAt = SYSUTCDATETIME()
                WHERE Code <> N'PK' AND IsDefault = 1;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("WhatsAppPakistanDefaultMigration applied — PK is default; UAE deactivated.");
    }
}
