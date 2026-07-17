using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Infrastructure.SignalR;
using SheikhTravelSystem.Infrastructure.Traccar;

namespace SheikhTravelSystem.API.Controllers;

[ApiController]
[Route("api/ops")]
[Authorize]
public class OpsController(ITraccarSyncState syncState, ITraccarClient traccar) : ControllerBase
{
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(CancellationToken cancellationToken)
    {
        var traccarConnected = false;
        var deviceCount = 0;
        try
        {
            var devices = await traccar.GetDevicesAsync(cancellationToken);
            traccarConnected = true;
            deviceCount = devices.Count;
        }
        catch
        {
            // leave defaults — health endpoint has detail
        }

        var sync = syncState.Snapshot(traccarConnected);
        return Ok(new
        {
            signalR = new { connectedClients = TrackingHubMetrics.ConnectedClients },
            traccar = new
            {
                connected = sync.Connected,
                enabled = sync.Enabled,
                isRunning = sync.IsRunning,
                deviceCount,
                lastPositionSyncAt = sync.LastPositionSyncAt,
                lastDeviceSyncAt = sync.LastDeviceSyncAt,
                lastEventSyncAt = sync.LastEventSyncAt,
                lastSyncCompletedAt = sync.LastSyncCompletedAt,
                lastError = sync.LastError,
                positionSyncIntervalSeconds = sync.PositionSyncIntervalSeconds
            },
            timestamp = DateTime.UtcNow
        });
    }
}
