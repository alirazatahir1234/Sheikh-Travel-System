using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Customers.Commands;
using SheikhTravelSystem.Application.Features.Customers.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class CustomersController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetCustomersQuery query)
        => Ok(await Mediator.Send(query));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command)
        => Ok(await Mediator.Send(command));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));
}
