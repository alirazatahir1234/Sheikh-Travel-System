using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence helpers for GPS devices and trackers. SQL lives in Infrastructure.
/// </summary>
public interface IGpsDeviceRepository
{
    Task<int> CountTraccarLinkedAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> CancelDeviceCommandAsync(CancelDeviceCommandCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> CompleteDeviceCommandAsync(CompleteDeviceCommandCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GpsDeviceDto>>> GetGpsDevicesAsync(GetGpsDevicesQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GpsDeviceCommandDto>>> GetDeviceCommandsAsync(GetDeviceCommandsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GpsDeviceCommandDetailDto>> GetDeviceCommandByIdAsync(GetDeviceCommandByIdQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GpsDeviceCommandDto>>> GetVehicleCommandsAsync(GetVehicleCommandsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GpsDeviceCommandDto>>> GetPendingDeviceCommandsAsync(GetPendingDeviceCommandsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<TrackerBrandDto>>> GetTrackerBrandsAsync(GetTrackerBrandsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<TrackerModelDto>>> GetTrackerModelsAsync(GetTrackerModelsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<TrackerDetailDto>>> GetTrackersAsync(GetTrackersQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TrackerDetailDto>> GetTrackerByIdAsync(GetTrackerByIdQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<TrackerAssignmentDto>>> GetTrackerAssignmentsAsync(GetTrackerAssignmentsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<TrackerInstallVehicleDto>>> GetTrackerInstallVehiclesAsync(GetTrackerInstallVehiclesQuery request, CancellationToken cancellationToken = default);

    Task<ApiResponse<int>> CreateGpsDeviceAsync(CreateGpsDeviceCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateGpsDeviceAsync(UpdateGpsDeviceCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteGpsDeviceAsync(DeleteGpsDeviceCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> SendDeviceCommandAsync(SendDeviceCommandCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> RetryDeviceCommandAsync(RetryDeviceCommandCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<SupportedCommandDto>>> GetDeviceSupportedCommandsAsync(GetDeviceSupportedCommandsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TraccarSyncRunResult>> SyncTrackerAsync(SyncTrackerCommand request, CancellationToken cancellationToken = default);
}
