using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence;

public sealed class DevDataService(IDbConnectionFactory dbFactory) : IDevDataService
{
    public async Task<int> ResetAdminPasswordAsync(string passwordHash, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET PasswordHash = @Hash, IsActive = 1, IsDeleted = 0 WHERE Email = @Email",
                new { Hash = passwordHash, Email = "admin@sheikhtravel.com" },
                cancellationToken: cancellationToken));
    }

    public async Task<DevDriverLoginFixResult?> FixDriverLoginAsync(
        string passwordHash, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE Users SET PasswordHash = @Hash, IsActive = 1, IsDeleted = 0
            WHERE Email = 'driver@sheikhtravel.com';

            UPDATE d SET d.UserId = u.Id, d.Phone = u.Phone, d.IsActive = 1
            FROM Drivers d
            INNER JOIN Users u ON u.Email = 'driver@sheikhtravel.com' AND u.IsDeleted = 0
            WHERE d.IsDeleted = 0
              AND d.Id = (SELECT MIN(Id) FROM Drivers WHERE IsDeleted = 0);
            """,
            new { Hash = passwordHash },
            cancellationToken: cancellationToken));

        return await connection.QuerySingleOrDefaultAsync<DevDriverLoginFixResult>(new CommandDefinition(
            """
            SELECT d.Id AS DriverId, d.Phone AS DriverPhone, d.UserId, u.Phone AS UserPhone
            FROM Drivers d
            LEFT JOIN Users u ON u.Id = d.UserId
            WHERE d.IsDeleted = 0 AND d.UserId IS NOT NULL
            ORDER BY d.Id
            OFFSET 0 ROWS FETCH NEXT 1 ROW ONLY
            """,
            cancellationToken: cancellationToken));
    }

    public async Task<int> MigrateBookingNumberAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // Add column if not already present
        var columnExists = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'BookingNumber'",
                cancellationToken: cancellationToken));

        if (columnExists == 0)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "ALTER TABLE Bookings ADD BookingNumber NVARCHAR(20) NOT NULL DEFAULT ''",
                    cancellationToken: cancellationToken));
        }

        // Backfill existing rows that have an empty BookingNumber
        var rows = (await connection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT Id FROM Bookings WHERE BookingNumber = '' OR BookingNumber IS NULL",
                cancellationToken: cancellationToken))).ToList();

        foreach (var id in rows)
        {
            var year = DateTime.UtcNow.Year;
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE Bookings SET BookingNumber = @BN WHERE Id = @Id",
                    new { BN = $"BK-{year}-{id:D4}", Id = id },
                    cancellationToken: cancellationToken));
        }

        return rows.Count;
    }
}
