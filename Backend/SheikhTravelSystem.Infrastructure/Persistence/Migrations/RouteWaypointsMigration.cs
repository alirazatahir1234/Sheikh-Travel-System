using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds waypoint and optimize-mode columns so planned corridor stops survive create/edit.
/// </summary>
public static class RouteWaypointsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH('Routes', 'WaypointsJson') IS NULL
                ALTER TABLE Routes ADD WaypointsJson NVARCHAR(MAX) NULL;

            IF COL_LENGTH('Routes', 'OptimizeMode') IS NULL
                ALTER TABLE Routes ADD OptimizeMode NVARCHAR(50) NULL;
            """, cancellationToken: cancellationToken));

        logger.LogInformation("RouteWaypointsMigration applied.");
    }
}
