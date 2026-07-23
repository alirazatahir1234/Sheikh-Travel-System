using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Development/QA system maintenance endpoints. Blocked outside Development and Staging.
/// </summary>
[ApiController]
[Route("api/admin/system")]
[Authorize]
public class AdminSystemController(
    IDatabaseResetService databaseResetService,
    IPlatformScope platformScope,
    ICurrentUserService currentUser,
    IWebHostEnvironment environment,
    ILogger<AdminSystemController> logger) : ControllerBase
{
    [HttpGet("reset-database/availability")]
    [RequirePermission(PlatformPermissions.SystemReset)]
    public IActionResult GetResetAvailability()
    {
        if (!IsResetEnvironment())
            return Ok(new { available = false, environment = environment.EnvironmentName });

        if (!platformScope.IsSuperAdmin)
            return Ok(new { available = false, environment = environment.EnvironmentName, reason = "Super Admin required." });

        return Ok(new { available = true, environment = environment.EnvironmentName });
    }

    [HttpPost("reset-database")]
    [RequirePermission(PlatformPermissions.SystemReset)]
    public async Task<IActionResult> ResetDatabase(
        [FromBody] ResetDatabaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsResetEnvironment())
            return Forbid();

        if (!platformScope.IsSuperAdmin)
            return Forbid();

        if (!string.Equals(request.Confirmation?.Trim(), "RESET", StringComparison.Ordinal))
            return BadRequest(ApiResponse<object>.FailResponse("Confirmation text must be RESET."));

        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var machineName = Environment.MachineName;

        logger.LogWarning(
            "Reset database requested by user {UserId} from {IpAddress}",
            userId, ipAddress);

        var result = await databaseResetService.ResetAsync(
            userId, ipAddress, machineName, cancellationToken);

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            success = result.Success,
            message = result.Message,
            deletedCompanies = result.DeletedCompanies,
            deletedUsers = result.DeletedUsers,
            deletedTrips = result.DeletedTrips,
            deletedVehicles = result.DeletedVehicles,
            deletedBookings = result.DeletedBookings,
            deletedDrivers = result.DeletedDrivers,
            deletedCustomers = result.DeletedCustomers,
            environment = environment.EnvironmentName,
            performedBy = userId,
            performedAt = DateTime.UtcNow,
            ipAddress,
            machineName
        }, result.Message));
    }

    private bool IsResetEnvironment() =>
        environment.IsDevelopment() || environment.IsStaging();
}

public record ResetDatabaseRequest(string? Confirmation);
