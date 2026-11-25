using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Queries;

public record GetTravelVisaCasesByCustomerQuery(int CustomerId) : IRequest<ApiResponse<IReadOnlyList<TravelVisaCaseListItemDto>>>;

public class GetTravelVisaCasesByCustomerQueryHandler(
    ITravelVisaCaseRepository repository,
    ITravelCustomerRepository customers,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<GetTravelVisaCasesByCustomerQuery, ApiResponse<IReadOnlyList<TravelVisaCaseListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelVisaCaseListItemDto>>> Handle(GetTravelVisaCasesByCustomerQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var customer = await customers.GetRefAsync(request.CustomerId, tenantId, cancellationToken);
        if (customer is null || !scope.Allows(customer.BranchId))
            throw new NotFoundException("TravelCustomer", request.CustomerId);

        return ApiResponse<IReadOnlyList<TravelVisaCaseListItemDto>>.SuccessResponse(
            await repository.GetByCustomerAsync(request.CustomerId, tenantId, cancellationToken));
    }
}
