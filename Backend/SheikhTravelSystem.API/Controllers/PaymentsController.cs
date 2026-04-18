using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Payments.Commands;
using SheikhTravelSystem.Application.Features.Payments.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class PaymentsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetPaymentsQuery query)
        => Ok(await Mediator.Send(query));

    [HttpGet("report")]
    public async Task<IActionResult> GetReport([FromQuery] GetPaymentReportQuery query)
        => Ok(await Mediator.Send(query));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePaymentCommand command)
        => Ok(await Mediator.Send(command));
}
