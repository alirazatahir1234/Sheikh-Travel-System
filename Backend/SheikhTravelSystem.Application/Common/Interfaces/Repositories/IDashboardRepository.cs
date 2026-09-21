using SheikhTravelSystem.Application.Features.Dashboard.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IDashboardRepository
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
