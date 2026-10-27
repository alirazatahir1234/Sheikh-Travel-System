using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Trips.Commands;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Application.Features.Trips.Queries;
using BulkImportBody = SheikhTravelSystem.Application.Features.Trips.Commands.BulkImportTripsCommand;
using SheikhTravelSystem.Application.Features.Trips.SmartAssignment;
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
        [FromQuery] TripStatus[]? statuses = null,
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
            page, pageSize, status, statuses, driverId, vehicleId, routeId, customerId,
            dateFrom, dateTo, search, todayOnly, tomorrowOnly, upcomingOnly)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetTripByIdQuery(id)));

    [HttpGet("{id:int}/route")]
    public async Task<IActionResult> RouteSummary(int id)
        => Ok(await Mediator.Send(new GetTripRouteSummaryQuery(id)));

    [RequirePermission(OperationsPermissions.TripUpdate)]
    [HttpPost("{id:int}/optimize-route")]
    public async Task<IActionResult> OptimizeRoute(int id)
        => Ok(await Mediator.Send(new OptimizeTripRouteCommand(id)));

    [RequirePermission(OperationsPermissions.TripCreate)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTripCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    [RequirePermission(OperationsPermissions.TripCreate)]
    [HttpPost("from-booking/{bookingId:int}")]
    public async Task<IActionResult> CreateFromBooking(int bookingId)
    {
        var result = await Mediator.Send(new CreateTripFromBookingCommand(bookingId));
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    [RequirePermission(OperationsPermissions.TripUpdate)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTripCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [RequirePermission(OperationsPermissions.TripUpdate)]
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTripStatusCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [RequirePermission(OperationsPermissions.TripAssign)]
    [HttpPut("{id:int}/assign-driver")]
    public async Task<IActionResult> AssignDriver(int id, [FromBody] AssignTripDriverDto body)
        => Ok(await Mediator.Send(new AssignTripDriverCommand(id, body.DriverId, body.AssistantDriverId, body.DriverNotes)));

    [RequirePermission(OperationsPermissions.TripAssign)]
    [HttpPut("{id:int}/assign-vehicle")]
    public async Task<IActionResult> AssignVehicle(int id, [FromBody] AssignTripVehicleDto body)
        => Ok(await Mediator.Send(new AssignTripVehicleCommand(id, body.VehicleId)));

    [RequirePermission(OperationsPermissions.TripDelete)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteTripCommand(id)));

    // ── Phase 2: expenses / passengers / documents ──────────────────────────

    [RequirePermission(OperationsPermissions.TripUpdate)]
    [HttpPost("{id:int}/expenses")]
    public async Task<IActionResult> AddExpense(int id, [FromBody] CreateTripExpenseDto body)
        => Ok(await Mediator.Send(new AddTripExpenseCommand(id, body)));

    [RequirePermission(OperationsPermissions.TripUpdate)]
    [HttpDelete("{id:int}/expenses/{expenseId:int}")]
    public async Task<IActionResult> DeleteExpense(int id, int expenseId)
        => Ok(await Mediator.Send(new DeleteTripExpenseCommand(id, expenseId)));

    [RequirePermission(OperationsPermissions.TripUpdate)]
    [HttpPost("{id:int}/passengers")]
    public async Task<IActionResult> AddPassenger(int id, [FromBody] CreateTripPassengerDto body)
        => Ok(await Mediator.Send(new AddTripPassengerCommand(id, body)));

    [RequirePermission(OperationsPermissions.TripUpdate)]
    [HttpPut("{id:int}/passengers/{passengerId:int}")]
    public async Task<IActionResult> UpdatePassenger(int id, int passengerId, [FromBody] UpdateTripPassengerDto body)
        => Ok(await Mediator.Send(new UpdateTripPassengerCommand(id, passengerId, body)));

    [RequirePermission(OperationsPermissions.TripUpdate)]
    [HttpDelete("{id:int}/passengers/{passengerId:int}")]
    public async Task<IActionResult> DeletePassenger(int id, int passengerId)
        => Ok(await Mediator.Send(new DeleteTripPassengerCommand(id, passengerId)));

    [RequirePermission(OperationsPermissions.TripUpdate)]
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

    [RequirePermission(OperationsPermissions.TripUpdate)]
    [HttpDelete("{id:int}/documents/{documentId:int}")]
    public async Task<IActionResult> DeleteDocument(int id, int documentId)
        => Ok(await Mediator.Send(new DeleteTripDocumentCommand(id, documentId)));

    // ── Smart assignment (AI-weighted driver + route matching) ───────────────

    [HttpGet("smart-assignment/recent")]
    public async Task<IActionResult> RecentSmartAssignments([FromQuery] int limit = 50)
        => Ok(await Mediator.Send(new GetRecentTripSmartAssignmentsQuery(limit)));

    [HttpGet("{id:int}/smart-assignment/suggestions")]
    public async Task<IActionResult> SmartAssignmentSuggestions(int id)
        => Ok(await Mediator.Send(new GetTripSmartAssignmentSuggestionsQuery(id)));

    [HttpGet("{id:int}/smart-assignment/decisions")]
    public async Task<IActionResult> SmartAssignmentDecisions(int id)
        => Ok(await Mediator.Send(new GetTripSmartAssignmentDecisionsQuery(id)));

    [RequirePermission(OperationsPermissions.TripAssign)]
    [HttpPost("{id:int}/smart-assignment/auto")]
    public async Task<IActionResult> SmartAssignmentAuto(int id)
        => Ok(await Mediator.Send(new AutoAssignTripSmartCommand(id)));

    [RequirePermission(OperationsPermissions.TripAssign)]
    [HttpPost("{id:int}/smart-assignment/override")]
    public async Task<IActionResult> SmartAssignmentOverride(int id, [FromBody] OverrideTripSmartAssignmentRequest body)
        => Ok(await Mediator.Send(new OverrideTripSmartAssignmentCommand(id, body)));

    // ── Trips tab enhancements (import, templates, insights, dispatch) ───────

    [RequirePermission(OperationsPermissions.TripCreate)]
    [HttpPost("import")]
    public async Task<IActionResult> BulkImport([FromBody] TripBulkImportRequest body)
        => Ok(await Mediator.Send(new BulkImportBody(body.CsvContent)));

    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates()
        => Ok(await Mediator.Send(new GetTripTemplatesQuery()));

    [RequirePermission(OperationsPermissions.TripCreate)]
    [HttpPost("templates")]
    public async Task<IActionResult> SaveTemplate([FromBody] UpsertTripTemplateDto body)
        => Ok(await Mediator.Send(new SaveTripTemplateCommand(body)));

    [RequirePermission(OperationsPermissions.TripDelete)]
    [HttpDelete("templates/{templateId:int}")]
    public async Task<IActionResult> DeleteTemplate(int templateId)
        => Ok(await Mediator.Send(new DeleteTripTemplateCommand(templateId)));

    [RequirePermission(OperationsPermissions.TripCreate)]
    [HttpPost("templates/{templateId:int}/spawn")]
    public async Task<IActionResult> SpawnFromTemplate(int templateId, [FromQuery] DateTime? date = null)
        => Ok(await Mediator.Send(new SpawnTripFromTemplateCommand(templateId, date)));

    [RequirePermission(OperationsPermissions.TripCreate)]
    [HttpPost("templates/spawn-due")]
    public async Task<IActionResult> SpawnDueRecurring()
        => Ok(await Mediator.Send(new SpawnDueRecurringTripsCommand()));

    [RequirePermission(OperationsPermissions.TripCreate)]
    [HttpPost("{id:int}/clone")]
    public async Task<IActionResult> Clone(int id)
        => Ok(await Mediator.Send(new CloneTripCommand(id)));

    [HttpGet("{id:int}/insights")]
    public async Task<IActionResult> Insights(int id)
        => Ok(await Mediator.Send(new GetTripInsightsQuery(id)));

    [HttpPost("estimate-cost")]
    public async Task<IActionResult> EstimateCost([FromBody] TripCostEstimateRequestDto body)
        => Ok(await Mediator.Send(new EstimateTripCostQuery(body)));

    [HttpGet("{id:int}/assignment-conflict")]
    public async Task<IActionResult> AssignmentConflict(int id, [FromQuery] int? driverId, [FromQuery] int? vehicleId)
        => Ok(await Mediator.Send(new GetTripAssignmentConflictQuery(id, driverId, vehicleId)));

    [RequirePermission(OperationsPermissions.TripUpdate)]
    [HttpPost("{id:int}/delayed-eta")]
    public async Task<IActionResult> SendDelayedEta(int id, [FromBody] SendDelayedEtaRequestDto body)
        => Ok(await Mediator.Send(new SendTripDelayedEtaCommand(id, body)));

    [RequirePermission(OperationsPermissions.TripUpdate)]
    [HttpPost("dispatch-board/move")]
    public async Task<IActionResult> MoveDispatchBoardTrip([FromBody] DispatchBoardMoveDto body)
        => Ok(await Mediator.Send(new MoveDispatchBoardTripCommand(body)));
}

public class TripBulkImportRequest
{
    public string CsvContent { get; set; } = "";
}

public class UploadTripDocumentForm
{
    public IFormFile? File { get; set; }
    public string? DocumentType { get; set; }
}
