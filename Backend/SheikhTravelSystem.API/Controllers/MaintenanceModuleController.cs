using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public partial class MaintenanceController : BaseApiController
{
    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] GetMaintenanceDashboardQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] GetMaintenanceAlertsQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests([FromQuery] ListMaintenanceRequestsQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("requests/stats")]
    public async Task<IActionResult> GetRequestStats()
        => Ok(await Mediator.Send(new GetMaintenanceRequestStatsQuery()));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("requests/{id:int}")]
    public async Task<IActionResult> GetRequestById(int id)
        => Ok(await Mediator.Send(new GetMaintenanceRequestByIdQuery(id)));

    [RequirePermission(MaintenancePermissions.RequestCreate)]
    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest([FromBody] CreateMaintenanceRequestDto body)
        => Ok(await Mediator.Send(new CreateMaintenanceRequestCommand(body)));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPut("requests/{id:int}")]
    public async Task<IActionResult> UpdateRequest(int id, [FromBody] UpdateMaintenanceRequestDto body)
        => Ok(await Mediator.Send(new UpdateMaintenanceRequestCommand(id, body)));

    [RequirePermission(MaintenancePermissions.WorkOrderManage)]
    [HttpPost("requests/{id:int}/convert")]
    public async Task<IActionResult> ConvertRequest(int id, [FromBody] ConvertRequestToWorkOrderDto body)
        => Ok(await Mediator.Send(new ConvertMaintenanceRequestCommand(id, body)));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] GetMaintenanceHistoryQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(MaintenancePermissions.ReportView)]
    [HttpGet("reports")]
    public async Task<IActionResult> GetReports([FromQuery] GetMaintenanceReportQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(MaintenancePermissions.ReportView)]
    [HttpGet("reports/schedules")]
    public async Task<IActionResult> GetReportSchedules()
        => Ok(await Mediator.Send(new ListMaintenanceReportSchedulesQuery()));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPost("reports/schedules")]
    public async Task<IActionResult> CreateReportSchedule([FromBody] CreateMaintenanceReportScheduleDto body)
        => Ok(await Mediator.Send(new CreateMaintenanceReportScheduleCommand(body)));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPut("reports/schedules/{id:int}")]
    public async Task<IActionResult> UpdateReportSchedule(int id, [FromBody] UpdateMaintenanceReportScheduleDto body)
        => Ok(await Mediator.Send(new UpdateMaintenanceReportScheduleCommand(id, body)));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpDelete("reports/schedules/{id:int}")]
    public async Task<IActionResult> DeleteReportSchedule(int id)
        => Ok(await Mediator.Send(new DeleteMaintenanceReportScheduleCommand(id)));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("schedules/calendar")]
    public async Task<IActionResult> GetScheduleCalendar([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await Mediator.Send(new GetMaintenanceScheduleCalendarQuery(from, to)));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("schedules/templates")]
    public async Task<IActionResult> GetScheduleTemplates()
        => Ok(await Mediator.Send(new GetMaintenanceScheduleTemplatesQuery()));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("schedules")]
    public async Task<IActionResult> GetSchedules([FromQuery] ListMaintenanceSchedulesQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPost("schedules")]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateMaintenanceScheduleDto body)
        => Ok(await Mediator.Send(new CreateMaintenanceScheduleCommand(body)));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPut("schedules/{id:int}/reschedule")]
    public async Task<IActionResult> RescheduleSchedule(int id, [FromBody] RescheduleMaintenanceScheduleDto body)
        => Ok(await Mediator.Send(new RescheduleMaintenanceScheduleCommand(id, body)));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPut("schedules/{id:int}")]
    public async Task<IActionResult> UpdateSchedule(int id, [FromBody] UpdateMaintenanceScheduleDto body)
        => Ok(await Mediator.Send(new UpdateMaintenanceScheduleCommand(id, body)));

    [RequirePermission(MaintenancePermissions.WorkOrderManage)]
    [HttpPost("schedules/{id:int}/work-order")]
    public async Task<IActionResult> CreateWorkOrderFromSchedule(int id)
        => Ok(await Mediator.Send(new CreateWorkOrderFromScheduleCommand(id)));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("service-types")]
    public async Task<IActionResult> GetServiceTypes()
        => Ok(await Mediator.Send(new ListServiceTypesQuery()));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("parts")]
    public async Task<IActionResult> GetParts([FromQuery] string? search)
        => Ok(await Mediator.Send(new ListPartsQuery(search)));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("parts/stats")]
    public async Task<IActionResult> GetPartsInventoryStats()
        => Ok(await Mediator.Send(new GetPartsInventoryStatsQuery()));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPost("parts")]
    public async Task<IActionResult> CreatePart([FromBody] CreatePartDto body)
        => Ok(await Mediator.Send(new CreatePartCommand(body)));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPost("parts/{id:int}/add-stock")]
    public async Task<IActionResult> AddPartStock(int id, [FromBody] AddPartStockDto body)
        => Ok(await Mediator.Send(new AddPartStockCommand(id, body)));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPost("parts/{id:int}/issue")]
    public async Task<IActionResult> IssuePart(int id, [FromBody] IssuePartDto body)
        => Ok(await Mediator.Send(new IssuePartCommand(id, body)));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPost("parts/{id:int}/transfer")]
    public async Task<IActionResult> TransferPartStock(int id, [FromBody] TransferPartStockDto body)
        => Ok(await Mediator.Send(new TransferPartStockCommand(id, body)));

    [RequirePermission(MaintenancePermissions.RequestApprove)]
    [HttpPost("requests/{id:int}/approve")]
    public async Task<IActionResult> ApproveRequest(int id)
        => Ok(await Mediator.Send(new ApproveMaintenanceRequestCommand(id)));

    [RequirePermission(MaintenancePermissions.RequestApprove)]
    [HttpPost("requests/{id:int}/reject")]
    public async Task<IActionResult> RejectRequest(int id, [FromBody] RejectMaintenanceRequestDto body)
        => Ok(await Mediator.Send(new RejectMaintenanceRequestCommand(id, body)));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] SearchMaintenanceQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPut("alerts/{id:int}/dismiss")]
    public async Task<IActionResult> DismissAlert(int id)
        => Ok(await Mediator.Send(new DismissMaintenanceAlertCommand(id)));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("compliance-summary")]
    public async Task<IActionResult> GetComplianceSummary()
        => Ok(await Mediator.Send(new GetMaintenanceComplianceSummaryQuery()));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("workshops-vendors/stats")]
    public async Task<IActionResult> GetWorkshopVendorStats()
        => Ok(await Mediator.Send(new GetWorkshopVendorStatsQuery()));

    [RequirePermission(MaintenancePermissions.Manage)]
    [HttpPost("requests/{id:int}/attachments")]
    public async Task<IActionResult> UploadAttachment(int id, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await Mediator.Send(new UploadMaintenanceRequestAttachmentCommand(
            id, stream, file.FileName, file.ContentType, file.Length), cancellationToken);
        return Ok(result);
    }
}
