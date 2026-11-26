using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// Reminder rules (a fixed catalogue the tenant tunes: lead times, channels, customer text) and the dispatch log. Reminders are sent
/// by a background pass; "run" executes the same pass for the caller's tenant now.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.ReminderView)]
[Route("api/travel/reminders")]
public class TravelRemindersController : BaseApiController
{
    [HttpGet("rules")]
    public async Task<IActionResult> GetRules() => Ok(await Mediator.Send(new GetTravelReminderRulesQuery()));

    [RequirePermission(TravelPermissions.ReminderManage)]
    [HttpPut("rules/{ruleType}")]
    public async Task<IActionResult> SaveRule(string ruleType, [FromBody] SaveTravelReminderRuleDto dto)
        => Ok(await Mediator.Send(new SaveTravelReminderRuleCommand(ruleType, dto)));

    /// <summary>Paged log. Filters: ruleType, status (Pending | Sent | Skipped | Failed), audience (Staff | Customer), from/to (created).</summary>
    [HttpGet("log")]
    public async Task<IActionResult> GetLog([FromQuery] GetTravelReminderLogQuery query) => Ok(await Mediator.Send(query));

    [RequirePermission(TravelPermissions.ReminderManage)]
    [HttpPost("run")]
    public async Task<IActionResult> Run() => Ok(await Mediator.Send(new RunTravelRemindersCommand()));
}
