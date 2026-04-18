using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Customers.Commands;
using SheikhTravelSystem.Application.Features.Customers.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Manages customer operations.
/// </summary>
public class CustomersController : BaseApiController
{
    /// <summary>
    /// Gets customers using filter and pagination criteria.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetCustomersQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>
    /// Creates a new customer.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }

    /// <summary>
    /// Updates an existing customer by identifier.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));
}
