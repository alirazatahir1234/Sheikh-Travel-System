using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.API.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/migrations")]
public class MigrationsController(
    IDatabaseMigrationRunner runner,
    ICurrentUserService currentUser) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
        => Ok(await runner.GetStatusAsync(ct));

    [HttpPost("apply-pending")]
    public async Task<IActionResult> ApplyPending(CancellationToken ct)
    {
        var appliedBy = currentUser.UserId is int userId
            ? $"MigrationManager:{userId}"
            : "MigrationManager";

        var result = await runner.ApplyPendingAsync(appliedBy, ct);

        if (!string.IsNullOrEmpty(result.FailedMigration))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, result);
        }

        return Ok(result);
    }
}
