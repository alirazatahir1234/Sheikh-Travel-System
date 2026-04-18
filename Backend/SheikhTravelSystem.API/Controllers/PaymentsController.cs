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
}
