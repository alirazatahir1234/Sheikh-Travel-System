using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Queries;

/// <summary>
/// Fetches a single customer (non-deleted) by identifier.
/// </summary>
public record GetCustomerByIdQuery(int Id) : IRequest<ApiResponse<CustomerDto>>;

public class GetCustomerByIdQueryHandler(ICustomerRepository customerRepository)
    : IRequestHandler<GetCustomerByIdQuery, ApiResponse<CustomerDto>>
{
    public async Task<ApiResponse<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(request.Id, cancellationToken);
        return ApiResponse<CustomerDto>.SuccessResponse(customer);
    }
}
