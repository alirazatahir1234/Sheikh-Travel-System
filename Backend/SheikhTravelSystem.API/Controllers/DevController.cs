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
public class DevController(
    IDatabaseSeeder seeder,
    IDevDataService devData,
    IPasswordHasher passwordHasher,
    IWebHostEnvironment env) : ControllerBase
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

        const string newPassword = "Pass@123";
        var hash = passwordHasher.Hash(newPassword);
        var affected = await devData.ResetAdminPasswordAsync(hash, cancellationToken);

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
        var row = await devData.FixDriverLoginAsync(hash, cancellationToken);

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

        var backfilled = await devData.MigrateBookingNumberAsync(cancellationToken);
        return Ok(ApiResponse<string>.SuccessResponse("ok", $"Migration complete. Backfilled {backfilled} rows."));
    }
}
