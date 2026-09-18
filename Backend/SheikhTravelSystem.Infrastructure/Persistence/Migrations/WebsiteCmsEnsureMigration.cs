using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Re-applies Website CMS schema/seed when <see cref="WebsiteCmsMigration"/> was marked applied
/// (or skipped) but tables are missing — same pattern as <see cref="UserPresenceEnsureMigration"/>.
/// </summary>
public static class WebsiteCmsEnsureMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.WebsiteFeatures', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: cancellationToken));

        if (exists == 1)
        {
            logger.LogInformation("WebsiteCmsEnsureMigration: WebsiteFeatures already exists.");
            return;
        }

        logger.LogWarning(
            "WebsiteCmsEnsureMigration: WebsiteFeatures missing — re-running WebsiteCmsMigration.");
        await WebsiteCmsMigration.ApplyAsync(dbFactory, logger, cancellationToken);
        logger.LogInformation("WebsiteCmsEnsureMigration applied successfully.");
    }
}
