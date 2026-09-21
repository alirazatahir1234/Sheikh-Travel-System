using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for GPS tracking queries/commands. SQL lives in Infrastructure.
/// Device/tracker persistence: <see cref="IGpsDeviceRepository"/>.
/// </summary>
public interface IGpsTrackingRepository
{
    Task<ApiResponse<IReadOnlyList<AnalyticsReportScheduleDto>>> ListAnalyticsReportSchedulesAsync(ListAnalyticsReportSchedulesQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> CreateAnalyticsReportScheduleAsync(CreateAnalyticsReportScheduleCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateAnalyticsReportScheduleAsync(UpdateAnalyticsReportScheduleCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteAnalyticsReportScheduleAsync(DeleteAnalyticsReportScheduleCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> CreateGeofenceAsync(CreateGeofenceCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateGeofenceAsync(UpdateGeofenceCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteGeofenceAsync(DeleteGeofenceCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> DuplicateGeofenceAsync(DuplicateGeofenceCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpsertGeofenceAssignmentsAsync(UpsertGeofenceAssignmentsCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteGeofenceAssignmentAsync(DeleteGeofenceAssignmentCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> CreateGpsAlertRuleAsync(CreateGpsAlertRuleCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> AcknowledgeGpsAlertAsync(AcknowledgeGpsAlertCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> MarkGpsAlertReadAsync(MarkGpsAlertReadCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ResolveGpsAlertAsync(ResolveGpsAlertCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ArchiveGpsAlertAsync(ArchiveGpsAlertCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteGpsAlertEventAsync(DeleteGpsAlertEventCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateAlertSettingsAsync(UpdateAlertSettingsCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<FleetUtilizationDto>> GetFleetUtilizationAsync(GetFleetUtilizationQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TrendsDto>> GetAnalyticsTrendsAsync(GetAnalyticsTrendsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GeofenceAnalyticsDto>> GetGeofenceAnalyticsAsync(GetGeofenceAnalyticsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AlertEventStatsDto>> GetAlertEventStatsAsync(GetAlertEventStatsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<HeatmapPointDto>>> GetPositionHeatmapAsync(GetPositionHeatmapQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GpsVehicleHealthDto>>> GetVehicleHealthScoreAsync(GetVehicleHealthScoreQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GeofenceDto>>> GetGeofencesAsync(GetGeofencesQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GeofenceAssignmentDto>>> GetGeofenceAssignmentsAsync(GetGeofenceAssignmentsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GeofenceStatsDto>> GetGeofenceStatsAsync(GetGeofenceStatsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GpsAlertEventDto>>> GetGeofenceEventsAsync(GetGeofenceEventsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GpsLiveFleetVehicleDto>>> GetGpsLiveFleetAsync(GetGpsLiveFleetQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GpsAlertRuleDto>>> GetGpsAlertRulesAsync(GetGpsAlertRulesQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GpsAlertEventDto>>> GetGpsAlertEventsAsync(GetGpsAlertEventsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GpsAlertEventDto>> GetGpsAlertEventByIdAsync(GetGpsAlertEventByIdQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GpsAlertStatsDto>> GetGpsAlertStatsAsync(GetGpsAlertStatsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<AlertSettingDto>>> GetAlertSettingsAsync(GetAlertSettingsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GpsEtaDto>> GetGpsEtaAsync(GetGpsEtaQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> GetGeofenceBreachCountAsync(GetGeofenceBreachCountQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TripDeviceContextDto>> GetTripContextAsync(GetTripContextQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GpsFleetStatusLocalDto>> GetGpsFleetStatusLocalAsync(GetGpsFleetStatusLocalQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GpsOperatorDashboardDto>> GetGpsOperatorDashboardAsync(GetGpsOperatorDashboardQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<GpsOperatorInsightDto>> PostGpsOperatorInsightsAsync(PostGpsOperatorInsightsCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<GpsFleetStatusSnapshotDto>>> GetGpsFleetStatusHistoryAsync(GetGpsFleetStatusHistoryQuery request, CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> IngestPositionAsync(IngestPositionCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<FuelAnalyticsDto>> GetFuelAnalyticsAsync(GetFuelAnalyticsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<CostAnalyticsDto>> GetCostAnalyticsAsync(GetCostAnalyticsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<DriverScoreDto>>> GetDriverScoreRankingAsync(GetDriverScoreRankingQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IdleAnalyticsDto>> GetIdleAnalyticsAsync(GetIdleAnalyticsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<StopAnalyticsDto>> GetStopAnalyticsAsync(GetStopAnalyticsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AnalyticsOverviewDto>> GetAnalyticsOverviewAsync(GetAnalyticsOverviewQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<VehicleRankingDto>>> GetVehicleRankingAsync(GetVehicleRankingQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PagedResult<PositionDto>>> GetLivePositionsAsync(GetLivePositionsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<PositionDto>>> GetPositionHistoryAsync(GetPositionHistoryQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<PagedResult<GpsTripDto>>> GetGpsTripsAsync(GetGpsTripsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<HistoryReplayBundleDto>> GetHistoryReplayAsync(GetHistoryReplayQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<HistoryExportFileDto>> GetHistoryExportAsync(GetHistoryExportQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TripAnalyticsBundleDto>> GetTripAnalyticsAsync(GetTripAnalyticsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TripReplayBundleDto>> GetTripReplayAsync(GetTripReplayQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<TripAnalyticsSummaryDto>> GetFleetTripSummaryAsync(GetFleetTripSummaryQuery request, CancellationToken cancellationToken = default);
}
