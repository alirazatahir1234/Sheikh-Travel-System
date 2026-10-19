using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Moves dead Active catalog modules RENTAL/HR to ComingSoon (no ERP route).
/// </summary>
public static class ModuleRentalHrComingSoonMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID('Modules', 'U') IS NULL RETURN;

            UPDATE Modules
            SET Status = 'ComingSoon', Route = NULL
            WHERE ModuleCode IN ('RENTAL', 'HR');
            """, cancellationToken: cancellationToken));

        logger.LogInformation("ModuleRentalHrComingSoonMigration applied (RENTAL, HR → ComingSoon).");
    }
}
