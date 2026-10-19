using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Settlements.Commands;
using SheikhTravelSystem.Application.Features.Settlements.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(FinancePermissions.SettlementView)]
public class SettlementsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetSettlementsQuery query)
        => Ok(await Mediator.Send(query));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetSettlementByIdQuery(id)));

    [RequirePermission(FinancePermissions.SettlementManage)]
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateDraft([FromBody] GenerateDraftSettlementCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }

    [RequirePermission(FinancePermissions.SettlementManage)]
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id)
        => Ok(await Mediator.Send(new ApproveSettlementCommand(id)));

    [RequirePermission(FinancePermissions.SettlementManage)]
    [HttpPost("{id:int}/mark-paid")]
    public async Task<IActionResult> MarkPaid(int id, [FromBody] MarkSettlementPaidRequest? body)
        => Ok(await Mediator.Send(new MarkSettlementPaidCommand(id, body?.Reference)));

    [RequirePermission(FinancePermissions.SettlementManage)]
    [HttpPost("{id:int}/adjust")]
    public async Task<IActionResult> Adjust(int id, [FromBody] AdjustSettlementRequest body)
        => Ok(await Mediator.Send(new AdjustSettlementCommand(id, body.Adjustments, body.Notes)));
}

public record MarkSettlementPaidRequest(string? Reference);
public record AdjustSettlementRequest(decimal Adjustments, string? Notes = null);
