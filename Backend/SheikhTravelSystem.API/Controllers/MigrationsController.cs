using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[Route("api/migrations")]
public class MigrationsController(
    IDatabaseMigrationRunner runner,
    ICurrentUserService currentUser) : BaseApiController
{
    [HttpGet]
    [RequirePermission(PlatformPermissions.MigrationsView)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
        => Ok(await runner.GetStatusAsync(ct));

    [HttpPost("apply-pending")]
    [RequirePermission(PlatformPermissions.MigrationsManage)]
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
