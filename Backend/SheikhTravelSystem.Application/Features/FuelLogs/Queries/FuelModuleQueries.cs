using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

namespace SheikhTravelSystem.Application.Features.FuelLogs.Queries;

public record GetVehicleFuelTelemetryQuery(int VehicleId) : IRequest<ApiResponse<VehicleFuelTelemetryDto>>;

public class GetVehicleFuelTelemetryQueryHandler(
    IFuelLogRepository fuelLogRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetVehicleFuelTelemetryQuery, ApiResponse<VehicleFuelTelemetryDto>>
{
    public async Task<ApiResponse<VehicleFuelTelemetryDto>> Handle(
        GetVehicleFuelTelemetryQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = await fuelLogRepository.GetVehicleTelemetryAsync(tenantId, request.VehicleId, cancellationToken);
        return dto is null
            ? ApiResponse<VehicleFuelTelemetryDto>.FailResponse("Vehicle not found.")
            : ApiResponse<VehicleFuelTelemetryDto>.SuccessResponse(dto);
    }
}

public record GetFuelEfficiencyReportQuery(DateTime? From = null, DateTime? To = null)
    : IRequest<ApiResponse<FuelEfficiencyReportDto>>;

public record GetFuelEfficiencyReportForRangeQuery(DateTime From, DateTime ToExclusive)
    : IRequest<ApiResponse<FuelEfficiencyReportDto>>;

public class GetFuelEfficiencyReportQueryHandler(
    IFuelLogRepository fuelLogRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetFuelEfficiencyReportQuery, ApiResponse<FuelEfficiencyReportDto>>,
        IRequestHandler<GetFuelEfficiencyReportForRangeQuery, ApiResponse<FuelEfficiencyReportDto>>
{
    public Task<ApiResponse<FuelEfficiencyReportDto>> Handle(
        GetFuelEfficiencyReportQuery request,
        CancellationToken cancellationToken)
    {
        var toExclusive = request.To?.Date.AddDays(1) ?? DateTime.UtcNow.Date.AddDays(1);
        var from = request.From?.Date
            ?? new DateTime(toExclusive.AddMonths(-1).Year, toExclusive.AddMonths(-1).Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (toExclusive <= from)
            toExclusive = from.AddDays(1);
        return HandleRange(from, toExclusive, cancellationToken);
    }

    public Task<ApiResponse<FuelEfficiencyReportDto>> Handle(
        GetFuelEfficiencyReportForRangeQuery request,
        CancellationToken cancellationToken)
        => HandleRange(request.From, request.ToExclusive, cancellationToken);

    private async Task<ApiResponse<FuelEfficiencyReportDto>> HandleRange(
        DateTime from, DateTime toExclusive, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var report = await fuelLogRepository.GetEfficiencyReportAsync(tenantId, from, toExclusive, cancellationToken);
        return ApiResponse<FuelEfficiencyReportDto>.SuccessResponse(report);
    }
}
