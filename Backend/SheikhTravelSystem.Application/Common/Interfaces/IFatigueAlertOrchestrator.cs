using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.FatigueDetection;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IFatigueAlertOrchestrator
{
    Task<ApiResponse<IngestFatigueSignalResultDto>> IngestAsync(
        int tenantId,
        int driverId,
        IngestFatigueSignalRequest request,
        CancellationToken ct = default);
}
