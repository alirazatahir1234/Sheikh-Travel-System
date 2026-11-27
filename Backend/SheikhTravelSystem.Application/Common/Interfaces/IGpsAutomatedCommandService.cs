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

    /// <summary>
    /// Puts a command for the vehicle's capable device in the approval queue (Status PendingApproval) without sending
    /// it. Nothing reaches the device until someone with command-approve rights approves; for an engine command the
    /// engine safety check runs at that point (it cannot run at queue time: the vehicle is usually moving).
    /// Refuses when the vehicle has no device that supports the command, or one is already waiting or pending.
    /// </summary>
    Task<ApiResponse<int>> QueueForApprovalAsync(
        int tenantId,
        int vehicleId,
        string commandType,
        string reason,
        string requestedBy,
        CancellationToken cancellationToken = default);
}
