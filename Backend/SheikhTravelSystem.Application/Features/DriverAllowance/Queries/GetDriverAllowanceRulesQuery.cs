using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverAllowance.Queries;

public record GetDriverAllowanceRulesQuery(int Page = 1, int PageSize = 50, bool ActiveOnly = false)
    : IRequest<ApiResponse<PagedResult<DriverAllowanceRuleDto>>>;

public class GetDriverAllowanceRulesQueryHandler(IDriverAllowanceRepository repository)
    : IRequestHandler<GetDriverAllowanceRulesQuery, ApiResponse<PagedResult<DriverAllowanceRuleDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverAllowanceRuleDto>>> Handle(
        GetDriverAllowanceRulesQuery request, CancellationToken cancellationToken)
    {
        var result = await repository.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.ActiveOnly,
            cancellationToken);

        return ApiResponse<PagedResult<DriverAllowanceRuleDto>>.SuccessResponse(result);
    }
}
