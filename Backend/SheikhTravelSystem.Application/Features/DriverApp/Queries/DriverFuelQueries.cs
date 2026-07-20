using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverApp.Queries;

public record GetDriverFuelReceiptsQuery(int Page = 1, int PageSize = 30)
    : IRequest<ApiResponse<List<DriverFuelReceiptDto>>>;

public class GetDriverFuelReceiptsQueryHandler(
    IDbConnectionFactory dbFactory,
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

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT f.Id, f.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehiclePlate,
                   f.Liters, f.PricePerLiter, f.TotalCost, f.OdometerReading, f.FuelType,
                   f.FuelDate, f.Station, f.ReceiptUrl
            FROM FuelLogs f
            LEFT JOIN Vehicles v ON v.Id = f.VehicleId
            WHERE f.DriverId = @DriverId AND f.IsDeleted = 0
            ORDER BY f.FuelDate DESC
            OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY
            """,
            new { DriverId = driverId.Value, Offset = offset, Size = pageSize },
            cancellationToken: cancellationToken));

        var list = rows.Select(r =>
        {
            var fuelType = (FuelType)(int)r.FuelType;
            var receipt = (string?)r.ReceiptUrl;
            return new DriverFuelReceiptDto(
                (int)r.Id,
                (int)r.VehicleId,
                (string?)r.VehicleName,
                (string?)r.VehiclePlate,
                (decimal)r.Liters,
                (decimal)r.PricePerLiter,
                (decimal)r.TotalCost,
                (decimal?)r.OdometerReading,
                fuelType.ToString(),
                (DateTime)r.FuelDate,
                (string?)r.Station,
                string.IsNullOrWhiteSpace(receipt) ? null : fileStorage.ResolveReadUrl(receipt));
        }).ToList();

        return ApiResponse<List<DriverFuelReceiptDto>>.SuccessResponse(list);
    }
}
