using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Fleet.EvAnomaly;

public record GetEvFleetDashboardQuery : IRequest<ApiResponse<EvFleetDashboardDto>>;

public record ListChargingStationsQuery : IRequest<ApiResponse<IReadOnlyList<ChargingStationDto>>>;

public record UpsertChargingStationCommand(UpsertChargingStationRequest Body)
    : IRequest<ApiResponse<int>>;

public record ListFleetAnomaliesQuery(bool UnreviewedOnly = false, int? VehicleId = null)
    : IRequest<ApiResponse<IReadOnlyList<FleetAnomalyEventDto>>>;

public record ReviewFleetAnomalyCommand(long AnomalyId, ReviewFleetAnomalyRequest Body)
    : IRequest<ApiResponse<bool>>;

public record GetVehicleDocumentTrackerQuery : IRequest<ApiResponse<IReadOnlyList<VehicleDocumentTrackerItemDto>>>;

public record UpsertVehicleComplianceDocumentCommand(UpsertComplianceDocumentRequest Body)
    : IRequest<ApiResponse<int>>;

public record GetDriverEvStatusQuery : IRequest<ApiResponse<DriverEvStatusDto>>;

public record GetDriverVehicleDocumentExpiriesQuery : IRequest<ApiResponse<IReadOnlyList<VehicleDocumentExpiryDto>>>;

public sealed class GetEvFleetDashboardQueryHandler(
    IFleetEvAnomalyRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetEvFleetDashboardQuery, ApiResponse<EvFleetDashboardDto>>
{
    public async Task<ApiResponse<EvFleetDashboardDto>> Handle(
        GetEvFleetDashboardQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = await repository.GetEvDashboardAsync(tenantId, cancellationToken);
        return ApiResponse<EvFleetDashboardDto>.SuccessResponse(dto);
    }
}

public sealed class ListChargingStationsQueryHandler(
    IFleetEvAnomalyRepository repository, ITenantContext tenantContext)
    : IRequestHandler<ListChargingStationsQuery, ApiResponse<IReadOnlyList<ChargingStationDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<ChargingStationDto>>> Handle(
        ListChargingStationsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.ListChargingStationsAsync(tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<ChargingStationDto>>.SuccessResponse(rows);
    }
}

public sealed class UpsertChargingStationCommandHandler(
    IFleetEvAnomalyRepository repository, ITenantContext tenantContext)
    : IRequestHandler<UpsertChargingStationCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        UpsertChargingStationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Body.Name))
            return ApiResponse<int>.FailResponse("Station name is required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var id = await repository.UpsertChargingStationAsync(tenantId, request.Body, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Charging station saved.");
    }
}

public sealed class ListFleetAnomaliesQueryHandler(
    IFleetEvAnomalyRepository repository, ITenantContext tenantContext)
    : IRequestHandler<ListFleetAnomaliesQuery, ApiResponse<IReadOnlyList<FleetAnomalyEventDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<FleetAnomalyEventDto>>> Handle(
        ListFleetAnomaliesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.ListAnomaliesAsync(
            tenantId, request.UnreviewedOnly, request.VehicleId, cancellationToken);
        return ApiResponse<IReadOnlyList<FleetAnomalyEventDto>>.SuccessResponse(rows);
    }
}

public sealed class ReviewFleetAnomalyCommandHandler(
    IFleetEvAnomalyRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<ReviewFleetAnomalyCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        ReviewFleetAnomalyCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString() ?? "user";
        var ok = await repository.ReviewAnomalyAsync(
            tenantId, request.AnomalyId, actor, request.Body.Notes, cancellationToken);
        return ok
            ? ApiResponse<bool>.SuccessResponse(true, "Anomaly reviewed.")
            : ApiResponse<bool>.FailResponse("Anomaly not found.");
    }
}

public sealed class GetVehicleDocumentTrackerQueryHandler(
    IFleetEvAnomalyRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleDocumentTrackerQuery, ApiResponse<IReadOnlyList<VehicleDocumentTrackerItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<VehicleDocumentTrackerItemDto>>> Handle(
        GetVehicleDocumentTrackerQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.GetVehicleDocumentTrackerAsync(tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<VehicleDocumentTrackerItemDto>>.SuccessResponse(rows);
    }
}

public sealed class UpsertVehicleComplianceDocumentCommandHandler(
    IFleetEvAnomalyRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<UpsertVehicleComplianceDocumentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        UpsertVehicleComplianceDocumentCommand request, CancellationToken cancellationToken)
    {
        if (request.Body.VehicleId <= 0 || string.IsNullOrWhiteSpace(request.Body.DocumentType))
            return ApiResponse<int>.FailResponse("Vehicle and document type are required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString();
        var id = await repository.UpsertComplianceDocumentAsync(tenantId, request.Body, actor, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Document saved.");
    }
}

public sealed class GetDriverEvStatusQueryHandler(
    IFleetEvAnomalyRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDriverEvStatusQuery, ApiResponse<DriverEvStatusDto>>
{
    public async Task<ApiResponse<DriverEvStatusDto>> Handle(
        GetDriverEvStatusQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverEvStatusDto>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = await repository.GetDriverEvStatusAsync(tenantId, driverId.Value, cancellationToken);
        return dto is null
            ? ApiResponse<DriverEvStatusDto>.FailResponse("No electric vehicle assigned.")
            : ApiResponse<DriverEvStatusDto>.SuccessResponse(dto);
    }
}

public sealed class GetDriverVehicleDocumentExpiriesQueryHandler(
    IFleetEvAnomalyRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDriverVehicleDocumentExpiriesQuery, ApiResponse<IReadOnlyList<VehicleDocumentExpiryDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<VehicleDocumentExpiryDto>>> Handle(
        GetDriverVehicleDocumentExpiriesQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<IReadOnlyList<VehicleDocumentExpiryDto>>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.GetDriverVehicleDocumentExpiriesAsync(tenantId, driverId.Value, cancellationToken);
        return ApiResponse<IReadOnlyList<VehicleDocumentExpiryDto>>.SuccessResponse(rows);
    }
}
