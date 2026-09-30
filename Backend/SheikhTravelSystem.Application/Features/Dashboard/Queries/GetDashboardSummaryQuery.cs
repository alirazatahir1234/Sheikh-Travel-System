using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Dashboard.DTOs;

namespace SheikhTravelSystem.Application.Features.Dashboard.Queries;

public record GetDashboardSummaryQuery : IRequest<ApiResponse<DashboardSummaryDto>>;

public class GetDashboardSummaryQueryHandler(
    IDashboardRepository dashboardRepository,
    ITenantContext tenantContext,
    IAppCache cache)
    : IRequestHandler<GetDashboardSummaryQuery, ApiResponse<DashboardSummaryDto>>
{
    public Task<ApiResponse<DashboardSummaryDto>> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return cache.GetOrCreateAsync(
            $"dashboard:summary:{tenantId}",
            AppCacheTtl.Dashboard,
            async ct =>
            {
                var summary = await dashboardRepository.GetSummaryAsync(tenantId, ct);
                return ApiResponse<DashboardSummaryDto>.SuccessResponse(summary);
            },
            cancellationToken);
    }
}
