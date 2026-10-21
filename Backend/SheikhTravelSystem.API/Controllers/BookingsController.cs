using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Application.Features.Bookings.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(OperationsPermissions.BookingView)]
/// <summary>
/// Manages booking operations.
/// </summary>
public class BookingsController : BaseApiController
{
    /// <summary>
    /// Gets bookings using filter and pagination criteria.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetBookingsQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>
    /// Soft-deletes multiple bookings in one request. Declared before POST create so routing stays unambiguous.
    /// </summary>
    [RequirePermission(OperationsPermissions.BookingDelete)]
    [HttpPost("bulk/delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteBookingsCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>
    /// Gets booking details by identifier.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetBookingByIdQuery(id)));

    /// <summary>
    /// Creates a new booking.
    /// </summary>
    [RequirePermission(OperationsPermissions.BookingCreate)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    /// <summary>
    /// Updates a booking.
    /// </summary>
    [RequirePermission(OperationsPermissions.BookingUpdate)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBookingCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>
    /// Updates booking status.
    /// </summary>
    [RequirePermission(OperationsPermissions.BookingUpdate)]
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>
    /// Assigns a driver to a booking.
    /// </summary>
    [RequirePermission(OperationsPermissions.BookingUpdate)]
    [HttpPut("{id:int}/assign-driver")]
    public async Task<IActionResult> AssignDriver(int id, [FromBody] AssignDriverCommand command)
        => Ok(await Mediator.Send(command with { BookingId = id }));

    /// <summary>
    /// Clears the driver assignment on a booking.
    /// </summary>
    [RequirePermission(OperationsPermissions.BookingUpdate)]
    [HttpPut("{id:int}/unassign-driver")]
    public async Task<IActionResult> UnassignDriver(int id)
        => Ok(await Mediator.Send(new UnassignDriverCommand(id)));

    /// <summary>
    /// Assigns a vehicle to a booking.
    /// </summary>
    [RequirePermission(OperationsPermissions.BookingUpdate)]
    [HttpPut("{id:int}/assign-vehicle")]
    public async Task<IActionResult> AssignVehicle(int id, [FromBody] AssignVehicleCommand command)
        => Ok(await Mediator.Send(command with { BookingId = id }));

    /// <summary>
    /// Clears the vehicle assignment on a booking.
    /// </summary>
    [RequirePermission(OperationsPermissions.BookingUpdate)]
    [HttpPut("{id:int}/unassign-vehicle")]
    public async Task<IActionResult> UnassignVehicle(int id)
        => Ok(await Mediator.Send(new UnassignVehicleCommand(id)));

    /// <summary>
    /// Soft-deletes a booking by identifier.
    /// </summary>
    [RequirePermission(OperationsPermissions.BookingDelete)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteBookingCommand(id)));
}
