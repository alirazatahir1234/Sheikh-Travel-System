using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Queries;

/// <summary>Current enquiry counts per status (KPI source — counters always sum to Total).</summary>
public record GetTravelEnquirySummaryQuery : IRequest<ApiResponse<TravelEnquirySummaryDto>>;

public class GetTravelEnquirySummaryQueryHandler(
    ITravelEnquiryRepository repository,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<GetTravelEnquirySummaryQuery, ApiResponse<TravelEnquirySummaryDto>>
{
    public async Task<ApiResponse<TravelEnquirySummaryDto>> Handle(GetTravelEnquirySummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var (counts, due) = await repository.GetSummaryAsync(tenantId, scope, DateTime.UtcNow, cancellationToken);
        return ApiResponse<TravelEnquirySummaryDto>.SuccessResponse(Build(counts, due));
    }

    /// <summary>Zero-fills every known status; Total is the sum of the buckets by construction.</summary>
    public static TravelEnquirySummaryDto Build(IReadOnlyDictionary<string, int> counts, int followUpsDue)
    {
        var filled = TravelValues.EnquiryStatuses.ToDictionary(s => s, s => counts.TryGetValue(s, out var n) ? n : 0);
        return new TravelEnquirySummaryDto { Counts = filled, Total = filled.Values.Sum(), FollowUpsDue = followUpsDue };
    }
}
