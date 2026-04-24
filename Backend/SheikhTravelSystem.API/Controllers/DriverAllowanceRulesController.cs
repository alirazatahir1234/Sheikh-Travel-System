using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.DriverAllowance.Commands;
using SheikhTravelSystem.Application.Features.DriverAllowance.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Admin CRUD for driver allowance rules plus the rule evaluator endpoint.
/// </summary>
public class DriverAllowanceRulesController : BaseApiController
{
    /// <summary>Lists configured rules (paged, newest priority first).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetDriverAllowanceRulesQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>Gets a single rule by identifier.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetDriverAllowanceRuleByIdQuery(id)));

    /// <summary>Creates a new driver allowance rule.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDriverAllowanceRuleCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }

    /// <summary>Updates an existing rule by identifier.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDriverAllowanceRuleCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Soft-deletes a rule by identifier.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteDriverAllowanceRuleCommand(id)));

    /// <summary>
    /// Evaluates rules against a booking context and returns the applicable
    /// allowance plus the applied rule metadata (auditable).
    /// </summary>
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] CalculateDriverAllowanceQuery query)
        => Ok(await Mediator.Send(query));
}
