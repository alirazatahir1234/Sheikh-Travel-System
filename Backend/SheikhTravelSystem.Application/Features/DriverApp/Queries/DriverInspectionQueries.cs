using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverApp.Queries;

public record GetDriverInspectionTemplateQuery : IRequest<ApiResponse<InspectionTemplateDto>>;

public class GetDriverInspectionTemplateQueryHandler(
    IDriverAppRepository driverAppRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverInspectionTemplateQuery, ApiResponse<InspectionTemplateDto>>
{
    public async Task<ApiResponse<InspectionTemplateDto>> Handle(
        GetDriverInspectionTemplateQuery request, CancellationToken cancellationToken)
    {
        var row = await driverAppRepository.GetInspectionTemplateAsync(
            tenantContext.GetRequiredTenantId(), cancellationToken);

        if (row is null)
            return ApiResponse<InspectionTemplateDto>.FailResponse("No inspection template configured.");

        return ApiResponse<InspectionTemplateDto>.SuccessResponse(new InspectionTemplateDto(
            row.Value.Id,
            row.Value.Name,
            row.Value.Description,
            InspectionResultCalculator.ParseChecklist(row.Value.ChecklistJson)));
    }
}

public record GetDriverInspectionHistoryQuery(int Page = 1, int PageSize = 30)
    : IRequest<ApiResponse<List<DriverInspectionSummaryDto>>>;

public class GetDriverInspectionHistoryQueryHandler(
    IDriverAppRepository driverAppRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverInspectionHistoryQuery, ApiResponse<List<DriverInspectionSummaryDto>>>
{
    public async Task<ApiResponse<List<DriverInspectionSummaryDto>>> Handle(
        GetDriverInspectionHistoryQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<List<DriverInspectionSummaryDto>>.FailResponse("Driver identity required.");

        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.PageSize is < 1 or > 100 ? 30 : request.PageSize;
        var offset = (page - 1) * size;

        var rows = await driverAppRepository.GetInspectionHistoryAsync(
            driverId.Value, tenantContext.GetRequiredTenantId(), offset, size, cancellationToken);

        var list = rows.Select(r =>
        {
            var photos = InspectionResultCalculator.ParsePhotos(r.PhotosJson);
            return new DriverInspectionSummaryDto(
                r.Id,
                r.VehicleId,
                r.VehicleName,
                r.VehiclePlate,
                r.InspectionDate,
                r.Result,
                r.OdometerReading,
                r.Comments,
                photos.Count,
                !string.IsNullOrWhiteSpace(r.SignatureUrl));
        }).ToList();

        return ApiResponse<List<DriverInspectionSummaryDto>>.SuccessResponse(list);
    }
}

public record GetDriverVehiclesForInspectionQuery : IRequest<ApiResponse<List<DriverInspectionVehicleDto>>>;

public record DriverInspectionVehicleDto(int Id, string Name, string? Plate);

public class GetDriverVehiclesForInspectionQueryHandler(
    IDriverAppRepository driverAppRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverVehiclesForInspectionQuery, ApiResponse<List<DriverInspectionVehicleDto>>>
{
    public async Task<ApiResponse<List<DriverInspectionVehicleDto>>> Handle(
        GetDriverVehiclesForInspectionQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<List<DriverInspectionVehicleDto>>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var list = (await driverAppRepository.GetVehiclesForInspectionAsync(
            driverId.Value, tenantId, cancellationToken)).ToList();

        if (list.Count == 0)
        {
            list = (await driverAppRepository.GetFallbackVehiclesForInspectionAsync(
                tenantId, cancellationToken)).ToList();
        }

        return ApiResponse<List<DriverInspectionVehicleDto>>.SuccessResponse(list);
    }
}
