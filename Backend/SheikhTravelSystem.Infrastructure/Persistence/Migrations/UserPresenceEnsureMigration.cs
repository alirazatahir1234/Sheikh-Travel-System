using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Ensures UserPresence exists even when AiPlatformMigration was marked applied
/// without creating the table (partial schema / drop / history drift).
/// </summary>
public static class UserPresenceEnsureMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.UserPresence', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: cancellationToken));

        if (exists == 1)
        {
            logger.LogInformation("UserPresenceEnsureMigration: table already exists.");
            return;
        }

        logger.LogInformation("Creating missing UserPresence table...");
        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE TABLE UserPresence (
                UserId INT NOT NULL PRIMARY KEY,
                BrowserOnline BIT NOT NULL DEFAULT 0,
                MobileOnline BIT NOT NULL DEFAULT 0,
                LastBrowserAt DATETIME2 NULL,
                LastMobileAt DATETIME2 NULL,
                LastLoginAt DATETIME2 NULL,
                LastReadAt DATETIME2 NULL,
                UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_UserPresence_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
            );
            """, cancellationToken: cancellationToken));

        logger.LogInformation("UserPresenceEnsureMigration applied successfully.");
    }
}
