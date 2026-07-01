using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.Application.Features.DriverApp.Commands;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.DriverApp.Queries;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

namespace SheikhTravelSystem.API.Controllers;

[ApiController]
[Route("api/driver-app")]
public class DriverAppController : BaseApiController
{
    // ── Auth ──────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] DriverLoginRequest request)
        => Ok(await Mediator.Send(new DriverLoginCommand(request.Phone, request.Password)));

    // ── Profile & Dashboard ──────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
        => Ok(await Mediator.Send(new GetDriverProfileQuery()));

    [Authorize(Roles = "Driver")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
        => Ok(await Mediator.Send(new GetDriverDashboardQuery()));

    // ── Trips ────────────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("trips")]
    public async Task<IActionResult> GetTrips()
        => Ok(await Mediator.Send(new GetDriverTripsQuery()));

    [Authorize(Roles = "Driver")]
    [HttpPost("trips/{id:int}/start")]
    public async Task<IActionResult> StartTrip(int id)
        => Ok(await Mediator.Send(new DriverStartTripCommand(id)));

    [Authorize(Roles = "Driver")]
    [HttpPost("trips/{id:int}/complete")]
    public async Task<IActionResult> CompleteTrip(int id)
        => Ok(await Mediator.Send(new DriverCompleteTripCommand(id)));

    [Authorize(Roles = "Driver")]
    [HttpPost("trips/{id:int}/reject")]
    public async Task<IActionResult> RejectTrip(int id, [FromBody] string reason)
        => Ok(await Mediator.Send(new DriverRejectTripCommand(id, reason)));

    [Authorize(Roles = "Driver")]
    [HttpPost("trips/location")]
    public async Task<IActionResult> PostLocation([FromBody] DriverLocationDto location)
        => Ok(await Mediator.Send(new DriverPostLocationCommand(location)));

    [Authorize(Roles = "Driver")]
    [HttpPost("location/batch")]
    public async Task<IActionResult> PostLocationBatch([FromBody] DriverLocationBatchDto batch)
        => Ok(await Mediator.Send(new DriverPostLocationBatchCommand(batch.Positions)));

    // ── Attendance ───────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpPost("attendance/check-in")]
    public async Task<IActionResult> CheckIn([FromBody] DriverCheckInRequest request)
        => Ok(await Mediator.Send(new DriverCheckInCommand(request.Latitude, request.Longitude)));

    [Authorize(Roles = "Driver")]
    [HttpPost("attendance/check-out")]
    public async Task<IActionResult> CheckOut([FromBody] DriverCheckOutRequest request)
        => Ok(await Mediator.Send(new DriverCheckOutCommand(request.Latitude, request.Longitude)));

    [Authorize(Roles = "Driver")]
    [HttpGet("attendance/history")]
    public async Task<IActionResult> GetAttendanceHistory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
        => Ok(await Mediator.Send(new GetDriverAttendanceHistoryQuery(from, to, page, pageSize)));

    // ── Fuel ─────────────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpPost("fuel-receipts")]
    public async Task<IActionResult> SubmitFuelReceipt([FromBody] CreateFuelLogDto fuelLog)
        => Ok(await Mediator.Send(new DriverSubmitFuelReceiptCommand(fuelLog)));

    // ── Earnings ──────────────────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("earnings")]
    public async Task<IActionResult> GetEarnings([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetDriverEarningsQuery(from, to)));

    // ── Notifications (proxy) ────────────────────────────────────────────────

    [Authorize(Roles = "Driver")]
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await Mediator.Send(
            new SheikhTravelSystem.Application.Features.Notifications.Queries.GetNotificationsQuery(userId, page, pageSize));
        return Ok(result);
    }

    // ── App Version ───────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpGet("app-version")]
    public IActionResult GetAppVersion()
        => Ok(new { MinVersion = "1.0.0", LatestVersion = "1.0.0", ForceUpdate = false });
}
