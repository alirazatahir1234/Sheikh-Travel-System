using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Geofences workspace (list, detail, KPIs, visits report, editor, groups, types, bulk, import). Reads need GPS.View
/// (class level) and the Geofencing add-on; writes and editor tools also need Gps.GeofenceManage. The original
/// geofence routes (list, stats, create/update, assignments, states, events) stay as they are for other callers.
/// </summary>
public partial class GpsTrackingController
{
    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpGet("geofences/workspace")]
    public async Task<IActionResult> GetGeofenceWorkspace()
        => Ok(await Mediator.Send(new GetGeofenceWorkspaceQuery()));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpPost("geofences/workspace")]
    [RequirePermission(GpsPermissions.GeofenceManage)]
    public async Task<IActionResult> CreateGeofenceFromWorkspace([FromBody] SaveGeofenceDto geofence)
    {
        var result = await Mediator.Send(new SaveGeofenceCommand(null, geofence));
        return Created(string.Empty, result);
    }

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpPut("geofences/workspace/{id:int}")]
    [RequirePermission(GpsPermissions.GeofenceManage)]
    public async Task<IActionResult> UpdateGeofenceFromWorkspace(int id, [FromBody] SaveGeofenceDto geofence)
        => Ok(await Mediator.Send(new SaveGeofenceCommand(id, geofence)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpGet("geofences/{id:int}")]
    public async Task<IActionResult> GetGeofenceDetail(int id)
        => Ok(await Mediator.Send(new GetGeofenceDetailQuery(id)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpGet("geofences/summary")]
    public async Task<IActionResult> GetGeofenceSummary()
        => Ok(await Mediator.Send(new GetGeofenceSummaryQuery()));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpGet("geofences/{id:int}/inside")]
    public async Task<IActionResult> GetGeofenceInside(int id)
        => Ok(await Mediator.Send(new GetGeofenceInsideQuery(id)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpGet("geofences/{id:int}/activity")]
    public async Task<IActionResult> GetGeofenceActivity(int id, [FromQuery] DateOnly? day)
        => Ok(await Mediator.Send(new GetGeofenceActivityQuery(id, day)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpGet("geofences/visits")]
    public async Task<IActionResult> GetGeofenceVisits(
        [FromQuery] int? geofenceId,
        [FromQuery] string? vehicleIds,
        [FromQuery] int? driverId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
        => Ok(await Mediator.Send(new GetGeofenceVisitsQuery(geofenceId, vehicleIds, driverId, from, to, status, page, pageSize)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpGet("geofences/visits/summary")]
    public async Task<IActionResult> GetGeofenceVisitsSummary(
        [FromQuery] int? geofenceId,
        [FromQuery] string? vehicleIds,
        [FromQuery] int? driverId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
        => Ok(await Mediator.Send(new GetGeofenceVisitsSummaryQuery(geofenceId, vehicleIds, driverId, from, to)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpPost("geofences/test-shape")]
    [RequirePermission(GpsPermissions.GeofenceManage)]
    public async Task<IActionResult> TestGeofenceShape([FromBody] TestGeofenceShapeDto shape)
        => Ok(await Mediator.Send(new TestGeofenceShapeQuery(shape)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpPost("geofences/bulk")]
    [RequirePermission(GpsPermissions.GeofenceManage)]
    public async Task<IActionResult> BulkGeofenceAction([FromBody] BulkGeofenceActionDto body)
        => Ok(await Mediator.Send(new BulkGeofenceActionCommand(body)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpPost("geofences/import")]
    [RequirePermission(GpsPermissions.GeofenceManage)]
    public async Task<IActionResult> ImportGeofences([FromBody] ImportGeofencesDto body)
        => Ok(await Mediator.Send(new ImportGeofencesCommand(body)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpGet("geofences/groups")]
    public async Task<IActionResult> GetGeofenceGroups()
        => Ok(await Mediator.Send(new GetGeofenceGroupsQuery()));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpPost("geofences/groups")]
    [RequirePermission(GpsPermissions.GeofenceManage)]
    public async Task<IActionResult> CreateGeofenceGroup([FromBody] SaveGeofenceGroupDto group)
        => Ok(await Mediator.Send(new SaveGeofenceGroupCommand(null, group)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpPut("geofences/groups/{id:int}")]
    [RequirePermission(GpsPermissions.GeofenceManage)]
    public async Task<IActionResult> UpdateGeofenceGroup(int id, [FromBody] SaveGeofenceGroupDto group)
        => Ok(await Mediator.Send(new SaveGeofenceGroupCommand(id, group)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpDelete("geofences/groups/{id:int}")]
    [RequirePermission(GpsPermissions.GeofenceManage)]
    public async Task<IActionResult> DeleteGeofenceGroup(int id)
        => Ok(await Mediator.Send(new DeleteGeofenceGroupCommand(id)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpGet("geofences/types")]
    public async Task<IActionResult> GetGeofenceTypes()
        => Ok(await Mediator.Send(new GetGeofenceTypesQuery()));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpPost("geofences/types")]
    [RequirePermission(GpsPermissions.GeofenceManage)]
    public async Task<IActionResult> CreateGeofenceType([FromBody] CreateGeofenceTypeDto type)
        => Ok(await Mediator.Send(new CreateGeofenceTypeCommand(type)));

    [RequireFeature(GpsAddOnFeatureCatalog.Geofencing, "Geofencing")]
    [HttpGet("geofences/recipient-options")]
    [RequirePermission(GpsPermissions.GeofenceManage)]
    public async Task<IActionResult> GetGeofenceRecipientOptions()
        => Ok(await Mediator.Send(new GetGeofenceRecipientOptionsQuery()));
}
