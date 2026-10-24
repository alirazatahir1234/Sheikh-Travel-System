using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.SmartOps.Queries;

public record GetSmartAlertsQuery(int TenantId, SmartAlertRuleType? RuleType = null, int Limit = 50)
    : IRequest<ApiResponse<SmartAlertSummaryDto>>;

public class GetSmartAlertsQueryHandler(ISmartAlertService smartAlerts)
    : IRequestHandler<GetSmartAlertsQuery, ApiResponse<SmartAlertSummaryDto>>
{
    public async Task<ApiResponse<SmartAlertSummaryDto>> Handle(GetSmartAlertsQuery request, CancellationToken cancellationToken)
    {
        var data = await smartAlerts.GetActiveAlertsAsync(request.TenantId, request.RuleType, request.Limit, cancellationToken);
        return ApiResponse<SmartAlertSummaryDto>.SuccessResponse(data);
    }
}

public record GetSmartAlertNewCountQuery(int TenantId) : IRequest<ApiResponse<int>>;

public class GetSmartAlertNewCountQueryHandler(ISmartAlertService smartAlerts)
    : IRequestHandler<GetSmartAlertNewCountQuery, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(GetSmartAlertNewCountQuery request, CancellationToken cancellationToken)
    {
        var count = await smartAlerts.GetNewCountAsync(request.TenantId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(count);
    }
}
