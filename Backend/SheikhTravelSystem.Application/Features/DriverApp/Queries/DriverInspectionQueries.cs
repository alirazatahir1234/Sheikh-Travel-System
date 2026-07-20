using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using static SheikhTravelSystem.Application.Features.DriverApp.DriverAppSql;

namespace SheikhTravelSystem.Application.Features.DriverApp.Queries;

public record GetDriverInspectionTemplateQuery : IRequest<ApiResponse<InspectionTemplateDto>>;

public class GetDriverInspectionTemplateQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverInspectionTemplateQuery, ApiResponse<InspectionTemplateDto>>
{
    public async Task<ApiResponse<InspectionTemplateDto>> Handle(
        GetDriverInspectionTemplateQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(int Id, string Name, string? Description, string ChecklistJson)>(
            new CommandDefinition(
                @"SELECT TOP 1 Id, Name, Description, ChecklistJson
                  FROM InspectionTemplates
                  WHERE IsDeleted = 0 AND IsActive = 1
                    AND (TenantId IS NULL OR TenantId = @TenantId)
                  ORDER BY CASE WHEN Name LIKE N'%Standard%' THEN 0 ELSE 1 END, Id",
                new { TenantId = tenantContext.GetRequiredTenantId() },
                cancellationToken: cancellationToken));

        if (row.Id == 0)
            return ApiResponse<InspectionTemplateDto>.FailResponse("No inspection template configured.");

        return ApiResponse<InspectionTemplateDto>.SuccessResponse(new InspectionTemplateDto(
            row.Id,
            row.Name,
            row.Description,
            InspectionResultCalculator.ParseChecklist(row.ChecklistJson)));
    }
}

public record GetDriverInspectionHistoryQuery(int Page = 1, int PageSize = 30)
    : IRequest<ApiResponse<List<DriverInspectionSummaryDto>>>;

public class GetDriverInspectionHistoryQueryHandler(
    IDbConnectionFactory dbFactory,
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

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync(new CommandDefinition(
            @"SELECT i.Id, i.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehiclePlate,
                     i.InspectionDate, i.Result, i.OdometerReading, i.Comments,
                     i.PhotosJson, i.SignatureUrl
              FROM Inspections i
              LEFT JOIN Vehicles v ON v.Id = i.VehicleId
              WHERE i.DriverId = @DriverId AND i.IsDeleted = 0
                AND (i.TenantId IS NULL OR i.TenantId = @TenantId)
              ORDER BY i.InspectionDate DESC
              OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY",
            new
            {
                DriverId = driverId.Value,
                TenantId = tenantContext.GetRequiredTenantId(),
                Offset = offset,
                Size = size
            },
            cancellationToken: cancellationToken));

        var list = rows.Select(r =>
        {
            var photos = InspectionResultCalculator.ParsePhotos((string?)r.PhotosJson);
            var sig = (string?)r.SignatureUrl;
            return new DriverInspectionSummaryDto(
                (int)r.Id,
                (int)r.VehicleId,
                (string?)r.VehicleName,
                (string?)r.VehiclePlate,
                (DateTime)r.InspectionDate,
                (string)r.Result,
                (decimal?)r.OdometerReading,
                (string?)r.Comments,
                photos.Count,
                !string.IsNullOrWhiteSpace(sig));
        }).ToList();

        return ApiResponse<List<DriverInspectionSummaryDto>>.SuccessResponse(list);
    }
}

public record GetDriverVehiclesForInspectionQuery : IRequest<ApiResponse<List<DriverInspectionVehicleDto>>>;

public record DriverInspectionVehicleDto(int Id, string Name, string? Plate);

public class GetDriverVehiclesForInspectionQueryHandler(
    IDbConnectionFactory dbFactory,
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

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var rows = await connection.QueryAsync<DriverInspectionVehicleDto>(new CommandDefinition(
            $"""
            SELECT DISTINCT v.Id, v.Name, v.RegistrationNumber AS Plate FROM (
                {DriverAppSql.AssignedVehicleIdsUnion}
                UNION
                SELECT TOP 1 VehicleId FROM Bookings
                WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0 AND VehicleId IS NOT NULL
                ORDER BY PickupTime DESC
            ) x
            INNER JOIN Vehicles v ON v.Id = x.VehicleId AND v.IsDeleted = 0
            """,
            new { DriverId = driverId.Value, TenantId = tenantId },
            cancellationToken: cancellationToken));

        var list = rows.ToList();
        if (list.Count == 0)
        {
            // Fallback: any active fleet vehicle for the tenant (dev / unassigned)
            list = (await connection.QueryAsync<DriverInspectionVehicleDto>(new CommandDefinition(
                @"SELECT TOP 20 Id, Name, RegistrationNumber AS Plate
                  FROM Vehicles WHERE TenantId = @TenantId AND IsDeleted = 0 AND Status IN (1, 2)
                  ORDER BY Name",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();
        }

        return ApiResponse<List<DriverInspectionVehicleDto>>.SuccessResponse(list);
    }
}
