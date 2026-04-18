using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Application.Features.Bookings.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
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
    /// Gets booking details by identifier.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetBookingByIdQuery(id)));

    /// <summary>
    /// Creates a new booking.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    /// <summary>
    /// Updates booking status.
    /// </summary>
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>
    /// Assigns a driver to a booking.
    /// </summary>
    [HttpPut("{id}/assign-driver")]
    public async Task<IActionResult> AssignDriver(int id, [FromBody] AssignDriverCommand command)
        => Ok(await Mediator.Send(command with { BookingId = id }));

    /// <summary>
    /// Assigns a vehicle to a booking.
    /// </summary>
    [HttpPut("{id}/assign-vehicle")]
    public async Task<IActionResult> AssignVehicle(int id, [FromBody] AssignVehicleCommand command)
        => Ok(await Mediator.Send(command with { BookingId = id }));
}
