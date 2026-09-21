using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.MaintenanceModule;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetPositionHeatmapQuery(int[] VehicleIds, DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<List<HeatmapPointDto>>>;

/// <summary>
/// Vehicle-selection-bounded by design, not fleet-wide — GpsPositions has no TenantId column and is
/// indexed only on (VehicleId, RecordedAt), so a fleet-wide heatmap would mean either a full table
/// scan or an unbounded per-vehicle fan-out against the highest-write table in the schema. Capped at
/// MaxVehicles, each queried against the existing per-vehicle index exactly like Replay/History
/// already do it. 90-day retention (GpsSettings.PositionRetentionDays) is inherited, same as every
/// other position-based feature.
/// </summary>
public class GetPositionHeatmapQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetPositionHeatmapQuery, ApiResponse<List<HeatmapPointDto>>>
{
    public Task<ApiResponse<List<HeatmapPointDto>>> Handle(GetPositionHeatmapQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetPositionHeatmapAsync(request, cancellationToken);
}

public record GetVehicleHealthScoreQuery(int? BranchId = null, int? DepartmentId = null)
    : IRequest<ApiResponse<List<GpsVehicleHealthDto>>>;

/// <summary>
/// Zero new signal logic — composes MaintenanceScheduleHelper.ComputeStatus() (reused verbatim,
/// same code the Maintenance dashboard already uses) with Vehicles.InsuranceExpiryDate and
/// GpsDevices.WarrantyStart/End, all of which already exist. VehicleMaintenanceSchedules only
/// carries Date/Mileage intervals (no EngineHours columns despite the helper supporting that axis),
/// so EngineHours is passed as null here — consistent with the table's actual schema, not a bug.
/// </summary>
public class GetVehicleHealthScoreQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetVehicleHealthScoreQuery, ApiResponse<List<GpsVehicleHealthDto>>>
{
    public Task<ApiResponse<List<GpsVehicleHealthDto>>> Handle(GetVehicleHealthScoreQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetVehicleHealthScoreAsync(request, cancellationToken);
}

public record GetVehicleRankingQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null)
    : IRequest<ApiResponse<List<VehicleRankingDto>>>;

/// <summary>Extends the same Fuel+Maintenance per-vehicle cost join MaintenanceDashboardQueries.GetFuelSummaryAsync already uses, adding trip distance/speed for a cost/km ranking.</summary>
public class GetVehicleRankingQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetVehicleRankingQuery, ApiResponse<List<VehicleRankingDto>>>
{
    public Task<ApiResponse<List<VehicleRankingDto>>> Handle(GetVehicleRankingQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetVehicleRankingAsync(request, cancellationToken);
}
