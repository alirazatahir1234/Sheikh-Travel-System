using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverAllowance.Queries;

public record GetDriverAllowanceRuleByIdQuery(int Id) : IRequest<ApiResponse<DriverAllowanceRuleDto>>;

public class GetDriverAllowanceRuleByIdQueryHandler(IDriverAllowanceRepository repository)
    : IRequestHandler<GetDriverAllowanceRuleByIdQuery, ApiResponse<DriverAllowanceRuleDto>>
{
    public async Task<ApiResponse<DriverAllowanceRuleDto>> Handle(
        GetDriverAllowanceRuleByIdQuery request, CancellationToken cancellationToken)
    {
        var rule = await repository.GetByIdAsync(request.Id, cancellationToken);

        if (rule is null)
            throw new NotFoundException("DriverAllowanceRule", request.Id);

        return ApiResponse<DriverAllowanceRuleDto>.SuccessResponse(rule);
    }
}
