using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Trackers;

public interface ITrackerRegistrationService
{
    Task<ApiResponse<TrackerRegisteredDto>> RegisterAsync(RegisterTrackerDto dto, CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateAsync(int id, UpdateTrackerDto dto, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAsync(int id, CancellationToken ct = default);
}
