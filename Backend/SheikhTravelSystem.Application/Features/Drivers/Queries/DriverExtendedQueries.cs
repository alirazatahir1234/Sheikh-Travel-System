using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record DriverDocumentDto(
    int Id,
    string DocumentType,
    string? FileUrl,
    DateTime? ExpiryDate,
    string Status,
    DateTime CreatedAt);

public record GetDriverDocumentsQuery(int DriverId) : IRequest<ApiResponse<IReadOnlyList<DriverDocumentDto>>>;

public class GetDriverDocumentsQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<GetDriverDocumentsQuery, ApiResponse<IReadOnlyList<DriverDocumentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DriverDocumentDto>>> Handle(
        GetDriverDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Driver", request.DriverId);

        var rows = (await connection.QueryAsync<DriverDocumentDto>(
            new CommandDefinition(
                @"SELECT Id, DocumentType, FileUrl, ExpiryDate, Status, CreatedAt
                  FROM ComplianceDocuments
                  WHERE TenantId = @TenantId AND EntityType = N'Driver' AND EntityId = @DriverId AND IsDeleted = 0
                  ORDER BY CreatedAt DESC",
                new { TenantId = tenantId, request.DriverId },
                cancellationToken: cancellationToken)))
            .Select(row => string.IsNullOrWhiteSpace(row.FileUrl)
                ? row
                : row with { FileUrl = fileStorage.ResolveReadUrl(row.FileUrl) })
            .ToList();

        return ApiResponse<IReadOnlyList<DriverDocumentDto>>.SuccessResponse(rows);
    }
}

public record GetDriverTimelineQuery(int DriverId) : IRequest<ApiResponse<IReadOnlyList<DriverTimelineEventDto>>>;

public class GetDriverTimelineQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverTimelineQuery, ApiResponse<IReadOnlyList<DriverTimelineEventDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DriverTimelineEventDto>>> Handle(
        GetDriverTimelineQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var events = (await connection.QueryAsync<DriverTimelineEventDto>(
            new CommandDefinition(
                @"SELECT * FROM (
                    SELECT d.Id AS Id, N'Registered' AS EventType, N'Driver registered' AS Title,
                      CONCAT(N'Code: ', ISNULL(d.DriverCode, N'—')) AS Description, d.CreatedAt AS OccurredAt
                    FROM Drivers d
                    WHERE d.Id = @DriverId AND d.TenantId = @TenantId AND d.IsDeleted = 0
                    UNION ALL
                    SELECT c.Id, N'DocumentUploaded', CONCAT(N'Document uploaded: ', c.DocumentType),
                      NULL, c.CreatedAt
                    FROM ComplianceDocuments c
                    WHERE c.EntityType = N'Driver' AND c.EntityId = @DriverId AND c.TenantId = @TenantId AND c.IsDeleted = 0
                    UNION ALL
                    SELECT a.Id, N'AssignedVehicle', N'Vehicle assigned',
                      CONCAT(N'Assignment #', a.Id), a.StartAt
                    FROM AssignmentHistory a
                    WHERE a.DriverId = @DriverId AND a.TenantId = @TenantId AND a.IsDeleted = 0
                  ) timeline
                  ORDER BY OccurredAt DESC",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        return ApiResponse<IReadOnlyList<DriverTimelineEventDto>>.SuccessResponse(events);
    }
}

public record GetDriverActiveDutyQuery(int DriverId) : IRequest<ApiResponse<DriverActiveDutyDto>>;

public record DriverActiveDutyDto(
    IReadOnlyList<DriverTripSummaryDto> RecentTrips,
    int FuelLogCount,
    bool HasGpsAssignment);

public record DriverTripSummaryDto(int Id, string Status, DateTime? TripDate, string? Route);

public class GetDriverActiveDutyQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverActiveDutyQuery, ApiResponse<DriverActiveDutyDto>>
{
    public async Task<ApiResponse<DriverActiveDutyDto>> Handle(
        GetDriverActiveDutyQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var trips = (await connection.QueryAsync<DriverTripSummaryDto>(
            new CommandDefinition(
                @"SELECT TOP 5 b.Id, CAST(b.Status AS NVARCHAR(20)) AS Status, b.TripDate, r.Name AS Route
                  FROM Bookings b
                  LEFT JOIN Routes r ON r.Id = b.RouteId
                  WHERE b.DriverId = @DriverId AND b.TenantId = @TenantId AND b.IsDeleted = 0
                  ORDER BY b.TripDate DESC",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        var fuelCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM FuelLogs WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var hasGps = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM AssignmentHistory ah
                    INNER JOIN Vehicles v ON v.Id = ah.VehicleId
                    WHERE ah.DriverId = @DriverId AND ah.TenantId = @TenantId
                      AND ah.Status = N'Active' AND ah.IsDeleted = 0 AND v.GpsDeviceId IS NOT NULL
                  ) THEN 1 ELSE 0 END",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return ApiResponse<DriverActiveDutyDto>.SuccessResponse(
            new DriverActiveDutyDto(trips, fuelCount, hasGps));
    }
}
