using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.AccidentRisk.Queries;

public record GetAccidentRiskDashboardQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null) : IRequest<ApiResponse<AccidentRiskDashboardDto>>;

public class GetAccidentRiskDashboardQueryHandler(IAccidentRiskRepository repository)
    : IRequestHandler<GetAccidentRiskDashboardQuery, ApiResponse<AccidentRiskDashboardDto>>
{
    public Task<ApiResponse<AccidentRiskDashboardDto>> Handle(
        GetAccidentRiskDashboardQuery request,
        CancellationToken cancellationToken)
        => repository.GetDashboardAsync(request.FromUtc, request.ToUtc, cancellationToken);
}
