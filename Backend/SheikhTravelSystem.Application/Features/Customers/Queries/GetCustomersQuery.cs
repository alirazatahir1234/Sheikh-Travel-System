using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Queries;

public record GetCustomersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsActive = null,
    string? Recency = null
) : IRequest<ApiResponse<PagedResult<CustomerDto>>>;

public class GetCustomersQueryHandler(ICustomerRepository customerRepository)
    : IRequestHandler<GetCustomersQuery, ApiResponse<PagedResult<CustomerDto>>>
{
    public async Task<ApiResponse<PagedResult<CustomerDto>>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var result = await customerRepository.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.IsActive,
            request.Recency,
            cancellationToken);

        return ApiResponse<PagedResult<CustomerDto>>.SuccessResponse(result);
    }
}
