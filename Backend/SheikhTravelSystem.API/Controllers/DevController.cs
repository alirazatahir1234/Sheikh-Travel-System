using Dapper;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Development-only utilities. Every action returns 404 outside the Development
/// environment so these endpoints cannot be hit in staging or production.
/// </summary>
[ApiController]
[Route("api/dev")]
public class DevController(IDatabaseSeeder seeder, IDbConnectionFactory dbFactory, IPasswordHasher passwordHasher, IWebHostEnvironment env) : ControllerBase
{
    /// <summary>
    /// Runs the idempotent seeder against empty tables.
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> Seed(CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment()) return NotFound();

        await seeder.SeedAsync(cancellationToken);
        return Ok(ApiResponse<string>.SuccessResponse("ok", "Seeder executed. Empty tables were populated."));
    }

    /// <summary>
    /// Wipes every seedable table and reseeds from scratch. DESTRUCTIVE.
    /// </summary>
    [HttpPost("reseed")]
    public async Task<IActionResult> Reseed(CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment()) return NotFound();

        await seeder.ResetAndSeedAsync(cancellationToken);
        return Ok(ApiResponse<string>.SuccessResponse("ok", "Database wiped and reseeded."));
    }

    /// <summary>
    /// Resets admin password to Pass@123 and ensures user is active.
    /// </summary>
    [HttpPost("reset-admin")]
    public async Task<IActionResult> ResetAdmin(CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment()) return NotFound();

        using var connection = dbFactory.CreateConnection();
        const string newPassword = "Pass@123";
        var hash = passwordHasher.Hash(newPassword);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET PasswordHash = @Hash, IsActive = 1, IsDeleted = 0 WHERE Email = @Email",
                new { Hash = hash, Email = "admin@sheikhtravel.com" },
                cancellationToken: cancellationToken));

        return Ok(ApiResponse<string>.SuccessResponse($"Updated {affected} row(s). Admin password is now: {newPassword}"));
    }

    /// <summary>
    /// Links driver@ user to the first driver row, syncs phone, resets password to Pass@123.
    /// </summary>
    [HttpPost("fix-driver-login")]
    public async Task<IActionResult> FixDriverLogin(CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment()) return NotFound();

        const string newPassword = "Pass@123";
        var hash = passwordHasher.Hash(newPassword);

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
            new { Hash = hash },
            cancellationToken: cancellationToken));

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(
            """
            SELECT d.Id AS DriverId, d.Phone AS DriverPhone, d.UserId, u.Phone AS UserPhone
            FROM Drivers d
            LEFT JOIN Users u ON u.Id = d.UserId
            WHERE d.IsDeleted = 0 AND d.UserId IS NOT NULL
            ORDER BY d.Id
            OFFSET 0 ROWS FETCH NEXT 1 ROW ONLY
            """,
            cancellationToken: cancellationToken));

        return Ok(ApiResponse<object>.SuccessResponse(
            new { password = newPassword, linked = row },
            "Driver login fixed. Use the driver/user phone with Pass@123."));
    }

    /// <summary>
    /// Adds BookingNumber column to Bookings table and backfills existing rows. Idempotent.
    /// </summary>
    [HttpPost("migrate-booking-number")]
    public async Task<IActionResult> MigrateBookingNumber(CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment()) return NotFound();

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
        var rows = await connection.QueryAsync<int>(
            new CommandDefinition("SELECT Id FROM Bookings WHERE BookingNumber = '' OR BookingNumber IS NULL",
            cancellationToken: cancellationToken));

        foreach (var id in rows)
        {
            var year = DateTime.UtcNow.Year;
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE Bookings SET BookingNumber = @BN WHERE Id = @Id",
                    new { BN = $"BK-{year}-{id:D4}", Id = id },
                    cancellationToken: cancellationToken));
        }

        return Ok(ApiResponse<string>.SuccessResponse("ok", $"Migration complete. Backfilled {rows.Count()} rows."));
    }
}
