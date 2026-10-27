using SheikhTravelSystem.Application.Features.SosDispatch;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ISosAutoDispatchService
{
    Task<SosDispatchPlan> BuildPlanAsync(
        int tenantId,
        int driverId,
        int? vehicleId,
        double? latitude,
        double? longitude,
        string driverName,
        CancellationToken cancellationToken = default);
}
