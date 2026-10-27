using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.AccidentRisk;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IAccidentRiskRepository
{
    Task<ApiResponse<AccidentRiskDashboardDto>> GetDashboardAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverAccidentRiskDto>> GetHighRiskDriversAsync(
        int tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RouteAccidentRiskDto>> GetHighRiskRoutesAsync(
        int tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}
