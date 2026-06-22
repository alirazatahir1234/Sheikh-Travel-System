using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Assignments;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class AssignmentsController : BaseApiController
{
    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ListAssignmentsQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
        => Ok(await Mediator.Send(new GetAssignmentStatsQuery()));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar([FromQuery] GetAssignmentCalendarQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("utilization-report")]
    public async Task<IActionResult> GetUtilizationReport()
        => Ok(await Mediator.Send(new GetAssignmentUtilizationReportQuery()));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateAssignmentRequest body)
        => Ok(await Mediator.Send(new ValidateAssignmentQuery(body)));

    [RequirePermission(DriverPermissions.DriverView)]
    [HttpGet("{id:int}/changelog")]
    public async Task<IActionResult> GetChangelog(int id)
        => Ok(await Mediator.Send(new GetAssignmentChangelogQuery(id)));

    [RequirePermission(DriverPermissions.DriverAssign)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssignmentRequest body)
        => Ok(await Mediator.Send(new CreateAssignmentCommand(body)));

    [RequirePermission(DriverPermissions.DriverAssign)]
    [HttpPost("{id:int}/transfer")]
    public async Task<IActionResult> Transfer(int id, [FromBody] TransferAssignmentRequest body)
        => Ok(await Mediator.Send(new TransferAssignmentCommand(id, body)));

    [RequirePermission(DriverPermissions.DriverAssign)]
    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteAssignmentRequest body)
        => Ok(await Mediator.Send(new CompleteAssignmentCommand(id, body)));

    [RequirePermission(DriverPermissions.DriverAssign)]
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelAssignmentRequest body)
        => Ok(await Mediator.Send(new CancelAssignmentCommand(id, body)));

    [RequirePermission(DriverPermissions.DriverAssign)]
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveAssignmentRequest body)
        => Ok(await Mediator.Send(new ApproveAssignmentCommand(id, body)));

    [RequirePermission(DriverPermissions.DriverAssign)]
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectAssignmentRequest body)
        => Ok(await Mediator.Send(new RejectAssignmentCommand(id, body)));

    [RequirePermission(DriverPermissions.DriverAssign)]
    [HttpPost("bulk-complete")]
    public async Task<IActionResult> BulkComplete([FromBody] BulkAssignmentIdsRequest body)
        => Ok(await Mediator.Send(new BulkCompleteAssignmentsCommand(body)));

    [RequirePermission(DriverPermissions.DriverAssign)]
    [HttpPost("bulk-cancel")]
    public async Task<IActionResult> BulkCancel([FromBody] BulkAssignmentIdsRequest body)
        => Ok(await Mediator.Send(new BulkCancelAssignmentsCommand(body)));
}
