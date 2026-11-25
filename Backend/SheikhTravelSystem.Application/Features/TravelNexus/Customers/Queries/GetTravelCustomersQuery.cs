using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Customers.Queries;

public class GetTravelCustomersQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelCustomerListItemDto>>>
{
    public string? Search { get; set; }
    public string? CustomerKind { get; set; }
    public int? AgentUserId { get; set; }
    public int? BranchId { get; set; }
}

public class GetTravelCustomersQueryHandler(
    ITravelCustomerRepository repository,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<GetTravelCustomersQuery, ApiResponse<PagedResult<TravelCustomerListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelCustomerListItemDto>>> Handle(
        GetTravelCustomersQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var kind = TravelValues.IsOneOf(request.CustomerKind, TravelValues.CustomerKinds) ? request.CustomerKind : null;
        var result = await repository.GetPagedAsync(
            tenantId, scope, request.Page, request.PageSize,
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            kind, request.AgentUserId, request.BranchId, cancellationToken);
        return ApiResponse<PagedResult<TravelCustomerListItemDto>>.SuccessResponse(result);
    }
}
