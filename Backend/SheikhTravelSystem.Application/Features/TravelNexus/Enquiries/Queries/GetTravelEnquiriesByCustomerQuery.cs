using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Queries;

public record GetTravelEnquiriesByCustomerQuery(int CustomerId) : IRequest<ApiResponse<IReadOnlyList<TravelEnquiryListItemDto>>>;

public class GetTravelEnquiriesByCustomerQueryHandler(
    ITravelEnquiryRepository repository,
    ITravelCustomerRepository customers,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<GetTravelEnquiriesByCustomerQuery, ApiResponse<IReadOnlyList<TravelEnquiryListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelEnquiryListItemDto>>> Handle(GetTravelEnquiriesByCustomerQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var customer = await customers.GetRefAsync(request.CustomerId, tenantId, cancellationToken);
        if (customer is null || !scope.Allows(customer.BranchId))
            throw new NotFoundException("TravelCustomer", request.CustomerId);

        return ApiResponse<IReadOnlyList<TravelEnquiryListItemDto>>.SuccessResponse(
            await repository.GetByCustomerAsync(request.CustomerId, tenantId, cancellationToken));
    }
}
