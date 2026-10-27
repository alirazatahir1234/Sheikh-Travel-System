using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Queues GPS device commands from background automation (fatigue auto-stop, etc.) without ERP user permissions.
/// </summary>
public interface IGpsAutomatedCommandService
{
    Task<ApiResponse<int>> QueueAsync(
        int tenantId,
        int gpsDeviceId,
        string commandType,
        string reason,
        CancellationToken cancellationToken = default);
}
