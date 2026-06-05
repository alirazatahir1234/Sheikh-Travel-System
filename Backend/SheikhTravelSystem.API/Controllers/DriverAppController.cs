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
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] DriverLoginRequest request)
        => Ok(await Mediator.Send(new DriverLoginCommand(request.Phone, request.Password)));

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
    [HttpPost("fuel-receipts")]
    public async Task<IActionResult> SubmitFuelReceipt([FromBody] CreateFuelLogDto fuelLog)
        => Ok(await Mediator.Send(new DriverSubmitFuelReceiptCommand(fuelLog)));

    [Authorize(Roles = "Driver")]
    [HttpGet("earnings")]
    public async Task<IActionResult> GetEarnings([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetDriverEarningsQuery(from, to)));
}
