using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Queries;

/// <summary>Current visa case count per pipeline status (KPI source — counters always sum to Total).</summary>
public record GetTravelVisaCaseSummaryQuery : IRequest<ApiResponse<TravelVisaCaseSummaryDto>>;

public class GetTravelVisaCaseSummaryQueryHandler(
    ITravelVisaCaseRepository repository,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<GetTravelVisaCaseSummaryQuery, ApiResponse<TravelVisaCaseSummaryDto>>
{
    public async Task<ApiResponse<TravelVisaCaseSummaryDto>> Handle(GetTravelVisaCaseSummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var counts = await repository.GetStatusCountsAsync(tenantId, scope, cancellationToken);
        return ApiResponse<TravelVisaCaseSummaryDto>.SuccessResponse(Build(counts));
    }

    /// <summary>Zero-fills every known status; Total is the sum of the buckets by construction.</summary>
    public static TravelVisaCaseSummaryDto Build(IReadOnlyDictionary<string, int> counts)
    {
        var filled = TravelValues.VisaCaseStatuses.ToDictionary(s => s, s => counts.TryGetValue(s, out var n) ? n : 0);
        return new TravelVisaCaseSummaryDto { Counts = filled, Total = filled.Values.Sum() };
    }
}
