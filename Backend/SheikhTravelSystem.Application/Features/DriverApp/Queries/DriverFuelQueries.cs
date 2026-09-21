using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverApp.Queries;

public record GetDriverFuelReceiptsQuery(int Page = 1, int PageSize = 30)
    : IRequest<ApiResponse<List<DriverFuelReceiptDto>>>;

public class GetDriverFuelReceiptsQueryHandler(
    IDriverAppRepository driverAppRepository,
    ICurrentUserService currentUser,
    IFileStorageService fileStorage)
    : IRequestHandler<GetDriverFuelReceiptsQuery, ApiResponse<List<DriverFuelReceiptDto>>>
{
    public async Task<ApiResponse<List<DriverFuelReceiptDto>>> Handle(
        GetDriverFuelReceiptsQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<List<DriverFuelReceiptDto>>.FailResponse("Driver identity required.");

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 30 : request.PageSize;
        var offset = (page - 1) * pageSize;

        var rows = await driverAppRepository.GetFuelReceiptsAsync(driverId.Value, offset, pageSize, cancellationToken);

        var list = rows.Select(r =>
        {
            var fuelType = (FuelType)r.FuelType;
            return new DriverFuelReceiptDto(
                r.Id,
                r.VehicleId,
                r.VehicleName,
                r.VehiclePlate,
                r.Liters,
                r.PricePerLiter,
                r.TotalCost,
                r.OdometerReading,
                fuelType.ToString(),
                r.FuelDate,
                r.Station,
                string.IsNullOrWhiteSpace(r.ReceiptUrl) ? null : fileStorage.ResolveReadUrl(r.ReceiptUrl));
        }).ToList();

        return ApiResponse<List<DriverFuelReceiptDto>>.SuccessResponse(list);
    }
}
