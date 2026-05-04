using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Payments.Commands;
using SheikhTravelSystem.Application.Features.Payments.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Manages payment operations and reporting endpoints.
/// </summary>
public class PaymentsController : BaseApiController
{
    /// <summary>
    /// Gets payments using filter and pagination criteria.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetPaymentsQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>
    /// Gets a single payment with full booking/customer details.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetPaymentByIdQuery(id)));

    /// <summary>
    /// Gets payments for a specific booking.
    /// </summary>
    [HttpGet("booking/{bookingId:int}")]
    public async Task<IActionResult> GetByBooking(int bookingId)
        => Ok(await Mediator.Send(new GetPaymentsByBookingQuery(bookingId)));

    /// <summary>
    /// Gets payment report data.
    /// </summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetReport([FromQuery] GetPaymentReportQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>
    /// Creates a new payment.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePaymentCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }

    /// <summary>
    /// Updates the status of a payment (e.g. mark as Refunded).
    /// </summary>
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdatePaymentStatusCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));
}
