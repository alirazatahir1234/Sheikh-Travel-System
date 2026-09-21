using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Queries;

public record GetCustomerListStatsQuery(
    string? Search = null,
    bool? IsActive = null
) : IRequest<ApiResponse<CustomerListStatsDto>>;

public class GetCustomerListStatsQueryHandler(ICustomerRepository customerRepository)
    : IRequestHandler<GetCustomerListStatsQuery, ApiResponse<CustomerListStatsDto>>
{
    public async Task<ApiResponse<CustomerListStatsDto>> Handle(GetCustomerListStatsQuery request, CancellationToken cancellationToken)
    {
        var stats = await customerRepository.GetListStatsAsync(request.Search, request.IsActive, cancellationToken);
        return ApiResponse<CustomerListStatsDto>.SuccessResponse(stats);
    }
}
