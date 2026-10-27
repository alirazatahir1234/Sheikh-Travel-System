using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;

namespace SheikhTravelSystem.API.Controllers;

public partial class GpsTrackingController
{
    [RequirePermission(OperationsPermissions.TripView)]
    [HttpGet("client-tracking-links")]
    public async Task<IActionResult> ListClientTrackingLinks(
        [FromQuery] int take = 50,
        [FromQuery] int? tripId = null,
        [FromQuery] bool activeOnly = false)
        => Ok(await Mediator.Send(new ListClientTrackingLinksQuery(take, tripId, activeOnly)));

    [RequirePermission(OperationsPermissions.TripAssign)]
    [HttpPost("client-tracking-links")]
    public async Task<IActionResult> CreateClientTrackingLink([FromBody] CreateClientTrackingLinkRequest body)
        => Ok(await Mediator.Send(new CreateClientTrackingLinkCommand(body.TripId, body.HoursValid)));

    [RequirePermission(OperationsPermissions.TripAssign)]
    [HttpPost("client-tracking-links/{linkId:long}/revoke")]
    public async Task<IActionResult> RevokeClientTrackingLink(long linkId)
        => Ok(await Mediator.Send(new RevokeClientTrackingLinkCommand(linkId)));
}

public record CreateClientTrackingLinkRequest(int TripId, int? HoursValid);
