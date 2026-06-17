using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Models;
using SheikhTravelSystem.Application.Features.Vehicles.Commands;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Application.Features.Vehicles.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Manages vehicle operations.
/// </summary>
public class VehiclesController : BaseApiController
{
    /// <summary>
    /// Gets vehicles using filter and pagination criteria.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetVehiclesQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>
    /// Gets vehicle details by identifier.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetVehicleByIdQuery(id)));

    /// <summary>
    /// Creates a new vehicle.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVehicleApiRequest request)
    {
        var result = await Mediator.Send(new CreateVehicleCommand(request.Vehicle, request.SaveAsDraft));
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    /// <summary>
    /// Updates an existing vehicle by identifier.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVehicleCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>
    /// Deletes a vehicle by identifier.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteVehicleCommand(id)));

    /// <summary>
    /// Toggles vehicle status between Available and Retired.
    /// </summary>
    [HttpPatch("{id}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
        => Ok(await Mediator.Send(new ToggleVehicleStatusCommand(id)));

    [HttpGet("{id}/documents")]
    public async Task<IActionResult> GetDocuments(int id)
        => Ok(await Mediator.Send(new GetVehicleDocumentsQuery(id)));

    [HttpPost("{id}/documents")]
    public async Task<IActionResult> AddDocument(int id, [FromBody] CreateVehicleDocumentRequest body)
        => Ok(await Mediator.Send(new CreateVehicleDocumentCommand(
            id, body.DocumentType, body.FileUrl, body.ExpiryDate, body.Notes)));

    [HttpPost("{id}/documents/{documentId}/set-primary-image")]
    public async Task<IActionResult> SetPrimaryImage(int id, int documentId)
        => Ok(await Mediator.Send(new SetPrimaryVehicleImageCommand(id, documentId)));

    [HttpPost("{id}/documents/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(int id, [FromForm] UploadVehicleDocumentForm form)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest("File is required.");

        await using var stream = form.File.OpenReadStream();
        var result = await Mediator.Send(new UploadVehicleDocumentCommand(
            id,
            stream,
            form.File.FileName,
            form.File.ContentType ?? "application/octet-stream",
            form.DocumentType,
            form.ExpiryDate,
            form.Notes));
        return Ok(result);
    }

    [HttpPost("{id}/publish")]
    public async Task<IActionResult> Publish(int id)
        => Ok(await Mediator.Send(new PublishVehicleCommand(id)));

    [HttpGet("{id}/maintenance")]
    public async Task<IActionResult> GetMaintenance(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new GetVehicleMaintenanceQuery(id, page, pageSize)));

    [HttpGet("{id}/fuel")]
    public async Task<IActionResult> GetFuel(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new GetVehicleFuelQuery(id, page, pageSize)));

    [HttpGet("{id}/gps")]
    public async Task<IActionResult> GetGps(int id)
        => Ok(await Mediator.Send(new GetVehicleGpsQuery(id)));

    [HttpPost("{id}/change-status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeVehicleStatusRequest body)
        => Ok(await Mediator.Send(new ChangeVehicleStatusCommand(id, body)));

    [HttpPost("{id}/assign-driver")]
    public async Task<IActionResult> AssignDriver(int id, [FromBody] AssignVehicleDriverRequest body)
        => Ok(await Mediator.Send(new AssignVehicleDriverCommand(id, body)));

    [HttpPost("{id}/assign-gps")]
    public async Task<IActionResult> AssignGps(int id, [FromBody] AssignVehicleGpsRequest body)
        => Ok(await Mediator.Send(new AssignVehicleGpsCommand(id, body)));
}

public record CreateVehicleDocumentRequest(
    string DocumentType,
    string? FileUrl,
    DateTime? ExpiryDate,
    string? Notes);
