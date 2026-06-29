using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Drivers.Commands;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Application.Features.Drivers.Queries;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]

public class DriversController : BaseApiController
{
    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetDriversQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
        => Ok(await Mediator.Send(new GetDriverStatsQuery()));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailabilitySummary([FromQuery] int? branchId)
        => Ok(await Mediator.Send(new GetDriversAvailabilityQuery(branchId)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetDriverByIdQuery(id)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("check-availability")]
    public async Task<IActionResult> CheckAvailability([FromQuery] CheckDriverAvailabilityQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(DriverPermissions.DriverCreate)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDriverCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    [RequirePermission(DriverPermissions.DriverUpdate)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDriverCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [RequirePermission(DriverPermissions.DriverDelete)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteDriverCommand(id)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}/documents")]
    public async Task<IActionResult> GetDocuments(int id)
        => Ok(await Mediator.Send(new GetDriverDocumentsQuery(id)));

    [RequirePermission(DriverPermissions.DriverUpdate)]
    [HttpPost("{id:int}/documents/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(int id, [FromForm] UploadDriverDocumentForm form)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest("File is required.");

        await using var stream = form.File.OpenReadStream();
        var result = await Mediator.Send(new UploadDriverDocumentCommand(
            id, stream, form.File.FileName, form.File.ContentType ?? "application/octet-stream",
            form.DocumentType, form.ExpiryDate, form.File.Length));
        return Ok(result);
    }

    [RequirePermission(DriverPermissions.DriverUpdate)]
    [HttpPost("{id:int}/photo/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(int id, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        await using var stream = file.OpenReadStream();
        var result = await Mediator.Send(new UploadDriverPhotoCommand(
            id, stream, file.FileName, file.ContentType ?? "application/octet-stream", file.Length));
        return Ok(result);
    }

    [RequirePermission(DriverPermissions.DriverUpdate)]
    [HttpPatch("{id:int}/verification")]
    public async Task<IActionResult> UpdateVerification(int id, [FromBody] UpdateDriverVerificationRequest body)
        => Ok(await Mediator.Send(new UpdateDriverVerificationCommand(id, body.VerificationStatus)));

    [RequirePermission(DriverPermissions.DriverAssign)]
    [HttpPost("{id:int}/assign-vehicle")]
    public async Task<IActionResult> AssignVehicle(int id, [FromBody] AssignDriverVehicleRequest body)
        => Ok(await Mediator.Send(new AssignDriverVehicleCommand(id, body)));

    [RequirePermission(DriverPermissions.DriverAssign)]
    [HttpPost("{id:int}/unassign-vehicle")]
    public async Task<IActionResult> UnassignVehicle(int id)
        => Ok(await Mediator.Send(new UnassignDriverVehicleCommand(id)));

    [RequirePermission(DriverPermissions.DriverAssign)]
    [HttpPost("{id:int}/transfer-vehicle")]
    public async Task<IActionResult> TransferVehicle(int id, [FromBody] TransferDriverVehicleRequest body)
        => Ok(await Mediator.Send(new TransferDriverVehicleCommand(id, body)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}/assignments")]
    public async Task<IActionResult> GetAssignments(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new GetDriverAssignmentsQuery(id, page, pageSize)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}/availability")]
    public async Task<IActionResult> GetDriverAvailability(int id)
        => Ok(await Mediator.Send(new GetDriverAvailabilityDetailQuery(id)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}/timeline")]
    public async Task<IActionResult> GetTimeline(int id)
        => Ok(await Mediator.Send(new GetDriverTimelineQuery(id)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}/active-duty")]
    public async Task<IActionResult> GetActiveDuty(int id)
        => Ok(await Mediator.Send(new GetDriverActiveDutyQuery(id)));

    [RequirePermission(DriverPermissions.DriverViewPerformance)]
    [HttpGet("{id:int}/performance/summary")]
    public async Task<IActionResult> GetPerformanceSummary(int id, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        => Ok(await Mediator.Send(new GetDriverPerformanceSummaryQuery(id, fromDate, toDate)));

    [RequirePermission(DriverPermissions.DriverViewPerformance)]
    [HttpGet("{id:int}/performance")]
    public async Task<IActionResult> GetPerformance(int id, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        => Ok(await Mediator.Send(new GetDriverPerformanceSummaryQuery(id, fromDate, toDate)));

    [RequirePermission(DriverPermissions.DriverUpdate)]
    [HttpPatch("{id:int}/rating")]
    public async Task<IActionResult> UpdateRating(int id, [FromBody] UpdateDriverRatingRequest body)
        => Ok(await Mediator.Send(new UpdateDriverRatingCommand(id, body.Rating)));

    [RequirePermission(DriverPermissions.DriverViewPerformance)]
    [HttpGet("{id:int}/violations")]
    public async Task<IActionResult> GetViolations(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new GetDriverViolationsQuery(id, page, pageSize)));

    [RequirePermission(DriverPermissions.DriverUpdate)]
    [HttpPost("{id:int}/violations")]
    public async Task<IActionResult> CreateViolation(int id, [FromBody] CreateDriverViolationRequest body)
        => Ok(await Mediator.Send(new CreateDriverViolationCommand(id, body)));

    [RequirePermission(DriverPermissions.DriverViewPerformance)]
    [HttpGet("{id:int}/attendance")]
    public async Task<IActionResult> GetAttendance(int id, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        => Ok(await Mediator.Send(new GetDriverAttendanceQuery(id, fromDate, toDate)));

    [RequirePermission(DriverPermissions.DriverUpdate)]
    [HttpPost("{id:int}/attendance")]
    public async Task<IActionResult> CreateAttendance(int id, [FromBody] CreateDriverAttendanceRequest body)
        => Ok(await Mediator.Send(new CreateDriverAttendanceCommand(id, body)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}/location")]
    public async Task<IActionResult> GetLocation(int id)
        => Ok(await Mediator.Send(new GetDriverLocationQuery(id)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}/location/history")]
    public async Task<IActionResult> GetLocationHistory(int id, [FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await Mediator.Send(new GetDriverLocationHistoryQuery(id, from, to)));

    [RequirePermission(DriverPermissions.DriverManageStatus)]
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeDriverStatusRequest body)
        => Ok(await Mediator.Send(new ChangeDriverStatusCommand(id, body.Status)));

    [RequirePermission(DriverPermissions.DriverManageStatus)]
    [HttpPatch("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
        => Ok(await Mediator.Send(new ToggleDriverActiveCommand(id)));

    // ── Verification: per-document status ───────────────────────────────────

    [RequirePermission(DriverPermissions.DriverUpdate)]
    [HttpPatch("{id:int}/documents/{docId:int}/status")]
    public async Task<IActionResult> UpdateDocumentStatus(
        int id,
        int docId,
        [FromBody] UpdateDocumentStatusRequest body)
        => Ok(await Mediator.Send(new UpdateDocumentStatusCommand(id, docId, body.Status, body.RejectionReason)));

    // ── Verification: reviewer notes ─────────────────────────────────────────

    [RequirePermission(DriverPermissions.DriverUpdate)]
    [HttpPost("{id:int}/verification/review-notes")]
    public async Task<IActionResult> AddReviewNote(
        int id,
        [FromBody] AddDriverReviewNoteRequest body)
        => Ok(await Mediator.Send(new AddDriverReviewNoteCommand(id, body.Note, body.DocumentType)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}/verification/review-notes")]
    public async Task<IActionResult> GetReviewNotes(int id)
        => Ok(await Mediator.Send(new GetDriverReviewNotesQuery(id)));
}

public record UploadDriverDocumentForm(string DocumentType, DateTime? ExpiryDate, IFormFile? File);

public record ChangeDriverStatusRequest(DriverStatus Status);
