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
public class DevController(IDatabaseSeeder seeder, IWebHostEnvironment env) : ControllerBase
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
}
