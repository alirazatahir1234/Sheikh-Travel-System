using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>Adds password-reset token columns for self-service forgot password.</summary>
public static class PasswordResetTokenMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users')
            BEGIN
                IF COL_LENGTH('Users', 'PasswordResetTokenHash') IS NULL
                    ALTER TABLE Users ADD PasswordResetTokenHash NVARCHAR(128) NULL;
                IF COL_LENGTH('Users', 'PasswordResetTokenExpiryUtc') IS NULL
                    ALTER TABLE Users ADD PasswordResetTokenExpiryUtc DATETIME2 NULL;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("PasswordResetTokenMigration applied successfully.");
    }
}
