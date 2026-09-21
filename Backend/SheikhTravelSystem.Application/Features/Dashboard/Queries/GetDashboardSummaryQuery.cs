using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Dashboard.DTOs;

namespace SheikhTravelSystem.Application.Features.Dashboard.Queries;

public record GetDashboardSummaryQuery : IRequest<ApiResponse<DashboardSummaryDto>>;

public class GetDashboardSummaryQueryHandler(
    IDashboardRepository dashboardRepository,
    IAppCache cache)
    : IRequestHandler<GetDashboardSummaryQuery, ApiResponse<DashboardSummaryDto>>
{
    public Task<ApiResponse<DashboardSummaryDto>> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            "dashboard:summary",
            AppCacheTtl.Dashboard,
            async ct =>
            {
                var summary = await dashboardRepository.GetSummaryAsync(ct);
                return ApiResponse<DashboardSummaryDto>.SuccessResponse(summary);
            },
            cancellationToken);
}
