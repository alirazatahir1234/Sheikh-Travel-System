using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetDriverScoreRankingQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? VehicleId = null,
    /// <summary>
    /// When true, fan out to live Traccar stops/events for idle minutes and harsh fallback.
    /// Default false — Overall / Safety / Fuel / On-time KPIs use SQL (GpsAlertEvents + trips + fuel).
    /// </summary>
    bool IncludeTraccarEnrichment = false)
    : IRequest<ApiResponse<List<DriverScoreDto>>>;

/// <summary>
/// Attributes GpsTrips to drivers via an AssignmentHistory time-window overlap join (GpsTrips has no
/// DriverId column). Overspeed and harsh brake/accel come from GpsAlertEvents.DriverId (harsh
/// persisted by Traccar event sync). Live Traccar idle/events are opt-in via
/// <see cref="GetDriverScoreRankingQuery.IncludeTraccarEnrichment"/> — skipped on the KPI/list path.
/// Results are cached briefly per tenant + period + scope + weights.
/// </summary>
public class GetDriverScoreRankingQueryHandler(
    IGpsTrackingRepository gpsTrackingRepository,
    IDriverPerformanceScoringRepository scoringRepository,
    ITenantContext tenantContext,
    IAppCache cache)
    : IRequestHandler<GetDriverScoreRankingQuery, ApiResponse<List<DriverScoreDto>>>
{
    public const string CacheKeyPrefix = "gps:driver-ranking:";

    public async Task<ApiResponse<List<DriverScoreDto>>> Handle(
        GetDriverScoreRankingQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        // Weights change Overall — include fingerprint so a weight save cannot serve stale scores.
        var weights = await scoringRepository.GetOrCreateWeightsAsync(tenantId, cancellationToken);
        var weightKey =
            $"{weights.SafetyWeight:0.##}:{weights.FuelWeight:0.##}:{weights.OnTimeWeight:0.##}";
        var traccarKey = request.IncludeTraccarEnrichment ? "trc" : "sql";
        var cacheKey =
            $"{CacheKeyPrefix}{tenantId}:{fromDate:O}:{toDate:O}" +
            $":b{request.BranchId?.ToString() ?? "all"}" +
            $":d{request.DepartmentId?.ToString() ?? "all"}" +
            $":v{request.VehicleId?.ToString() ?? "all"}" +
            $":w{weightKey}:{traccarKey}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            AppCacheTtl.DriverScoreRanking,
            ct => gpsTrackingRepository.GetDriverScoreRankingAsync(request, ct),
            cancellationToken);
    }
}
