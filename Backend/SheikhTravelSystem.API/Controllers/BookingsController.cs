using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Application.Features.Bookings.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class BookingsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetBookingsQuery query)
        => Ok(await Mediator.Send(query));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetBookingByIdQuery(id)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpPut("{id}/assign-driver")]
    public async Task<IActionResult> AssignDriver(int id, [FromBody] AssignDriverCommand command)
        => Ok(await Mediator.Send(command with { BookingId = id }));

    [HttpPut("{id}/assign-vehicle")]
    public async Task<IActionResult> AssignVehicle(int id, [FromBody] AssignVehicleCommand command)
        => Ok(await Mediator.Send(command with { BookingId = id }));
}
