using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Ensures Bookings.BookingNumber exists and backfills empty values.
/// </summary>
public static class BookingNumberMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var columnExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'BookingNumber'",
            cancellationToken: cancellationToken));

        if (columnExists == 0)
        {
            logger.LogInformation("Adding BookingNumber column to Bookings table...");
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE Bookings ADD BookingNumber NVARCHAR(20) NOT NULL DEFAULT ''",
                cancellationToken: cancellationToken));
        }

        var rows = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Id FROM Bookings WHERE BookingNumber = '' OR BookingNumber IS NULL",
            cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
        {
            logger.LogInformation("BookingNumberMigration applied successfully.");
            return;
        }

        var year = DateTime.UtcNow.Year;
        foreach (var id in rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Bookings SET BookingNumber = @BN WHERE Id = @Id",
                new { BN = $"BK-{year}-{id:D4}", Id = id },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation("BookingNumberMigration applied successfully. Backfilled {Count} rows.", rows.Count);
    }
}
