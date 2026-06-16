using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.Application.Features.Vehicles.Queries;

public record GetVehicleMaintenanceQuery(int VehicleId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<VehicleMaintenanceDto>>>;

public class GetVehicleMaintenanceQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleMaintenanceQuery, ApiResponse<PagedResult<VehicleMaintenanceDto>>>
{
    public async Task<ApiResponse<PagedResult<VehicleMaintenanceDto>>> Handle(
        GetVehicleMaintenanceQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var vehicleExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = request.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!vehicleExists)
            throw new NotFoundException("Vehicle", request.VehicleId);

        var offset = (request.Page - 1) * request.PageSize;

        var records = await connection.QueryAsync<VehicleMaintenanceDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, Description, Cost, MaintenanceDate, NextDueDate,
                  Status, ServiceProvider, CreatedAt
                  FROM Maintenance
                  WHERE IsDeleted = 0 AND VehicleId = @VehicleId
                  ORDER BY MaintenanceDate DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { request.VehicleId, Offset = offset, request.PageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Maintenance WHERE IsDeleted = 0 AND VehicleId = @VehicleId",
                new { request.VehicleId },
                cancellationToken: cancellationToken));

        var result = new PagedResult<VehicleMaintenanceDto>
        {
            Items = records.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<VehicleMaintenanceDto>>.SuccessResponse(result);
    }
}

public record GetVehicleFuelQuery(int VehicleId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<VehicleFuelSummaryDto>>;

public class GetVehicleFuelQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleFuelQuery, ApiResponse<VehicleFuelSummaryDto>>
{
    public async Task<ApiResponse<VehicleFuelSummaryDto>> Handle(
        GetVehicleFuelQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var vehicleExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = request.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!vehicleExists)
            throw new NotFoundException("Vehicle", request.VehicleId);

        var offset = (request.Page - 1) * request.PageSize;

        var logs = await connection.QueryAsync<VehicleFuelDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, DriverId, Liters, PricePerLiter, TotalCost,
                  OdometerReading, FuelType, FuelDate, Station, CreatedAt
                  FROM FuelLogs
                  WHERE IsDeleted = 0 AND VehicleId = @VehicleId
                  ORDER BY FuelDate DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { request.VehicleId, Offset = offset, request.PageSize },
                cancellationToken: cancellationToken));

        var totals = await connection.QuerySingleAsync<(decimal TotalLiters, decimal TotalCost, int TotalCount)>(
            new CommandDefinition(
                @"SELECT ISNULL(SUM(Liters), 0) AS TotalLiters,
                         ISNULL(SUM(TotalCost), 0) AS TotalCost,
                         COUNT(*) AS TotalCount
                  FROM FuelLogs WHERE IsDeleted = 0 AND VehicleId = @VehicleId",
                new { request.VehicleId },
                cancellationToken: cancellationToken));

        var result = new VehicleFuelSummaryDto(logs.ToList(), totals.TotalLiters, totals.TotalCost, totals.TotalCount);
        return ApiResponse<VehicleFuelSummaryDto>.SuccessResponse(result);
    }
}

public record GetVehicleGpsQuery(int VehicleId) : IRequest<ApiResponse<VehicleGpsDto>>;

public class GetVehicleGpsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleGpsQuery, ApiResponse<VehicleGpsDto>>
{
    public async Task<ApiResponse<VehicleGpsDto>> Handle(
        GetVehicleGpsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var gps = await connection.QuerySingleOrDefaultAsync<VehicleGpsDto>(
            new CommandDefinition(
                @"SELECT v.GpsDeviceId,
                         gd.Name AS DeviceName, gd.UniqueId, gd.IsActive, gd.LastSeenAt, gd.LastIgnition,
                         vcl.Latitude, vcl.Longitude, vcl.Speed, vcl.LastUpdate
                  FROM Vehicles v
                  LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
                  LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                  WHERE v.Id = @VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0",
                new { request.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (gps is null)
            throw new NotFoundException("Vehicle", request.VehicleId);

        return ApiResponse<VehicleGpsDto>.SuccessResponse(gps);
    }
}
