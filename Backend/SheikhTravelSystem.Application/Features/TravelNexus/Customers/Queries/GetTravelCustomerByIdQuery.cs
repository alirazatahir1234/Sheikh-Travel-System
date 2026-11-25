using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Customers.Queries;

public record GetTravelCustomerByIdQuery(int CustomerId) : IRequest<ApiResponse<TravelCustomerDto>>;

public class GetTravelCustomerByIdQueryHandler(
    ITravelCustomerRepository repository,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetTravelCustomerByIdQuery, ApiResponse<TravelCustomerDto>>
{
    public async Task<ApiResponse<TravelCustomerDto>> Handle(GetTravelCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var customer = await repository.GetByIdAsync(request.CustomerId, tenantId, scope, cancellationToken)
            ?? throw new NotFoundException("TravelCustomer", request.CustomerId);

        if (!currentUser.HasPermission(TravelPermissions.TravelerViewPii) && customer.Cnic is not null)
        {
            customer.Cnic = TravelPii.Mask(customer.Cnic);
            customer.PiiMasked = true;
        }

        return ApiResponse<TravelCustomerDto>.SuccessResponse(customer);
    }
}
