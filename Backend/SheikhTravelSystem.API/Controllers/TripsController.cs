using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Trips.Commands;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Application.Features.Trips.Queries;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(OperationsPermissions.TripView)]
[Route("api/trips")]
public class TripsController : BaseApiController
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
        => Ok(await Mediator.Send(new GetTripDashboardQuery()));

    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await Mediator.Send(new GetTripCalendarQuery(from, to)));

    [HttpGet("live")]
    public async Task<IActionResult> Live([FromQuery] bool todayOnly = true)
        => Ok(await Mediator.Send(new GetLiveTripsQuery(todayOnly)));

    [HttpGet("analytics")]
    public async Task<IActionResult> Analytics([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        => Ok(await Mediator.Send(new GetTripAnalyticsQuery(from, to)));

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TripStatus? status = null,
        [FromQuery] int? driverId = null,
        [FromQuery] int? vehicleId = null,
        [FromQuery] int? routeId = null,
        [FromQuery] int? customerId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? search = null,
        [FromQuery] bool todayOnly = false,
        [FromQuery] bool tomorrowOnly = false,
        [FromQuery] bool upcomingOnly = false)
        => Ok(await Mediator.Send(new GetTripsQuery(
            page, pageSize, status, driverId, vehicleId, routeId, customerId,
            dateFrom, dateTo, search, todayOnly, tomorrowOnly, upcomingOnly)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetTripByIdQuery(id)));

    [HttpGet("{id:int}/route")]
    public async Task<IActionResult> RouteSummary(int id)
        => Ok(await Mediator.Send(new GetTripRouteSummaryQuery(id)));

    [HttpPost("{id:int}/optimize-route")]
    public async Task<IActionResult> OptimizeRoute(int id)
        => Ok(await Mediator.Send(new OptimizeTripRouteCommand(id)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTripCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    [HttpPost("from-booking/{bookingId:int}")]
    public async Task<IActionResult> CreateFromBooking(int bookingId)
    {
        var result = await Mediator.Send(new CreateTripFromBookingCommand(bookingId));
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTripCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTripStatusCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpPut("{id:int}/assign-driver")]
    public async Task<IActionResult> AssignDriver(int id, [FromBody] AssignTripDriverDto body)
        => Ok(await Mediator.Send(new AssignTripDriverCommand(id, body.DriverId, body.AssistantDriverId, body.DriverNotes)));

    [HttpPut("{id:int}/assign-vehicle")]
    public async Task<IActionResult> AssignVehicle(int id, [FromBody] AssignTripVehicleDto body)
        => Ok(await Mediator.Send(new AssignTripVehicleCommand(id, body.VehicleId)));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteTripCommand(id)));

    // ── Phase 2: expenses / passengers / documents ──────────────────────────

    [HttpPost("{id:int}/expenses")]
    public async Task<IActionResult> AddExpense(int id, [FromBody] CreateTripExpenseDto body)
        => Ok(await Mediator.Send(new AddTripExpenseCommand(id, body)));

    [HttpDelete("{id:int}/expenses/{expenseId:int}")]
    public async Task<IActionResult> DeleteExpense(int id, int expenseId)
        => Ok(await Mediator.Send(new DeleteTripExpenseCommand(id, expenseId)));

    [HttpPost("{id:int}/passengers")]
    public async Task<IActionResult> AddPassenger(int id, [FromBody] CreateTripPassengerDto body)
        => Ok(await Mediator.Send(new AddTripPassengerCommand(id, body)));

    [HttpPut("{id:int}/passengers/{passengerId:int}")]
    public async Task<IActionResult> UpdatePassenger(int id, int passengerId, [FromBody] UpdateTripPassengerDto body)
        => Ok(await Mediator.Send(new UpdateTripPassengerCommand(id, passengerId, body)));

    [HttpDelete("{id:int}/passengers/{passengerId:int}")]
    public async Task<IActionResult> DeletePassenger(int id, int passengerId)
        => Ok(await Mediator.Send(new DeleteTripPassengerCommand(id, passengerId)));

    [HttpPost("{id:int}/documents/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(int id, [FromForm] UploadTripDocumentForm form)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest("File is required.");

        await using var stream = form.File.OpenReadStream();
        return Ok(await Mediator.Send(new UploadTripDocumentCommand(
            id,
            stream,
            form.File.FileName,
            form.File.ContentType ?? "application/octet-stream",
            form.DocumentType ?? "Other",
            form.File.Length)));
    }

    [HttpDelete("{id:int}/documents/{documentId:int}")]
    public async Task<IActionResult> DeleteDocument(int id, int documentId)
        => Ok(await Mediator.Send(new DeleteTripDocumentCommand(id, documentId)));
}

public class UploadTripDocumentForm
{
    public IFormFile? File { get; set; }
    public string? DocumentType { get; set; }
}
