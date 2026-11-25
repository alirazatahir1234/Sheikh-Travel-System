using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Queries;

public record GetTravelTravelersByCustomerQuery(int CustomerId) : IRequest<ApiResponse<IReadOnlyList<TravelTravelerDto>>>;

public class GetTravelTravelersByCustomerQueryHandler(
    ITravelTravelerRepository repository,
    ITravelConfigRepository config,
    TravelTravelerGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetTravelTravelersByCustomerQuery, ApiResponse<IReadOnlyList<TravelTravelerDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelTravelerDto>>> Handle(
        GetTravelTravelersByCustomerQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        await guard.EnsureCustomerVisibleAsync(request.CustomerId, tenantId, scope, cancellationToken);

        var settings = await config.GetSettingsAsync(tenantId, cancellationToken);
        var canViewPii = currentUser.HasPermission(TravelPermissions.TravelerViewPii);
        var today = DateTime.UtcNow;
        var items = (await repository.GetByCustomerAsync(request.CustomerId, tenantId, cancellationToken))
            .Select(t => TravelTravelerPresenter.Present(t, canViewPii, today, settings.PassportExpiryWarningDays))
            .ToList();
        return ApiResponse<IReadOnlyList<TravelTravelerDto>>.SuccessResponse(items);
    }
}
