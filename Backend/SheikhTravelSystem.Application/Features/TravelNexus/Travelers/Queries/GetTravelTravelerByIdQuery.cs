using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Queries;

public record GetTravelTravelerByIdQuery(int Id) : IRequest<ApiResponse<TravelTravelerDto>>;

public class GetTravelTravelerByIdQueryHandler(
    ITravelTravelerRepository repository,
    ITravelConfigRepository config,
    TravelTravelerGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetTravelTravelerByIdQuery, ApiResponse<TravelTravelerDto>>
{
    public async Task<ApiResponse<TravelTravelerDto>> Handle(GetTravelTravelerByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var traveler = await repository.GetByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("TravelTraveler", request.Id);
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        await guard.EnsureCustomerVisibleAsync(traveler.CustomerId, tenantId, scope, cancellationToken);

        var settings = await config.GetSettingsAsync(tenantId, cancellationToken);
        return ApiResponse<TravelTravelerDto>.SuccessResponse(TravelTravelerPresenter.Present(
            traveler, currentUser.HasPermission(TravelPermissions.TravelerViewPii), DateTime.UtcNow, settings.PassportExpiryWarningDays));
    }
}
