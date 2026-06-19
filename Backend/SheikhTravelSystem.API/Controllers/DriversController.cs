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
/// <summary>
/// Manages driver operations.
/// </summary>
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

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}/timeline")]
    public async Task<IActionResult> GetTimeline(int id)
        => Ok(await Mediator.Send(new GetDriverTimelineQuery(id)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}/active-duty")]
    public async Task<IActionResult> GetActiveDuty(int id)
        => Ok(await Mediator.Send(new GetDriverActiveDutyQuery(id)));

    [RequirePermission(DriverPermissions.DriverUpdate)]
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeDriverStatusRequest body)
    {
        var driver = await Mediator.Send(new GetDriverByIdQuery(id));
        if (!driver.Success || driver.Data is null)
            return NotFound(driver);

        var current = driver.Data;
        var update = new UpdateDriverDto(
            current.FirstName ?? current.FullName.Split(' ')[0],
            current.LastName ?? (current.FullName.Contains(' ') ? current.FullName[(current.FullName.IndexOf(' ') + 1)..] : string.Empty),
            current.Phone,
            current.LicenseNumber,
            current.LicenseExpiryDate,
            body.Status,
            current.IsActive,
            current.Nationality,
            current.Email,
            current.HireDate,
            current.BranchId,
            current.DepartmentId,
            current.CNIC,
            current.Address);

        return Ok(await Mediator.Send(new UpdateDriverCommand(id, update)));
    }
}

public record UploadDriverDocumentForm(string DocumentType, DateTime? ExpiryDate, IFormFile? File);

public record ChangeDriverStatusRequest(DriverStatus Status);
