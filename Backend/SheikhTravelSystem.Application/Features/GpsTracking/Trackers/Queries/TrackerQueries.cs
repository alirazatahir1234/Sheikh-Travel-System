using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Queries;

public record GetTrackersQuery : IRequest<ApiResponse<List<TrackerDetailDto>>>;

public class GetTrackersQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetTrackersQuery, ApiResponse<List<TrackerDetailDto>>>
{
    public async Task<ApiResponse<List<TrackerDetailDto>>> Handle(GetTrackersQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await connection.QueryAsync<TrackerDetailDto>(new CommandDefinition(
            TrackerSql.ListQuery + TrackerTenantSql.DeviceScopeFilter + """
             ORDER BY CASE WHEN v.Name IS NULL THEN 1 ELSE 0 END, v.Name, d.Name
             """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        return ApiResponse<List<TrackerDetailDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetTrackerByIdQuery(int Id) : IRequest<ApiResponse<TrackerDetailDto>>;

public class GetTrackerByIdQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetTrackerByIdQuery, ApiResponse<TrackerDetailDto>>
{
    public async Task<ApiResponse<TrackerDetailDto>> Handle(GetTrackerByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await connection.QueryFirstOrDefaultAsync<TrackerDetailDto>(new CommandDefinition(
            TrackerSql.ListQuery + TrackerTenantSql.DeviceScopeFilter + " AND d.Id = @Id",
            new { request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return row is null
            ? ApiResponse<TrackerDetailDto>.FailResponse("Tracker not found.")
            : ApiResponse<TrackerDetailDto>.SuccessResponse(row);
    }
}

public record GetTrackerAssignmentsQuery(int TrackerId) : IRequest<ApiResponse<List<TrackerAssignmentDto>>>;

public class GetTrackerAssignmentsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetTrackerAssignmentsQuery, ApiResponse<List<TrackerAssignmentDto>>>
{
    public async Task<ApiResponse<List<TrackerAssignmentDto>>> Handle(GetTrackerAssignmentsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var allowed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            TrackerTenantSql.DeviceExistsForTenant,
            new { Id = request.TrackerId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (!allowed)
            return ApiResponse<List<TrackerAssignmentDto>>.FailResponse("Tracker not found.");

        var rows = await connection.QueryAsync<TrackerAssignmentDto>(new CommandDefinition(
            """
            SELECT a.Id, a.GpsDeviceId, a.VehicleId,
                   v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber,
                   a.DriverId, dr.FullName AS DriverName,
                   a.InstalledDate, a.RemovedDate, a.InstalledBy, a.RemovedBy, a.Reason, a.IsActive
            FROM GpsDeviceAssignments a
            INNER JOIN Vehicles v ON v.Id = a.VehicleId AND v.IsDeleted = 0
            LEFT JOIN Drivers dr ON dr.Id = a.DriverId AND dr.IsDeleted = 0
            WHERE a.GpsDeviceId = @TrackerId AND a.TenantId = @TenantId
            ORDER BY a.InstalledDate DESC
            """,
            new { request.TrackerId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<TrackerAssignmentDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetTrackerInstallVehiclesQuery(int? TrackerId = null) : IRequest<ApiResponse<List<TrackerInstallVehicleDto>>>;

public class GetTrackerInstallVehiclesQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetTrackerInstallVehiclesQuery, ApiResponse<List<TrackerInstallVehicleDto>>>
{
    public async Task<ApiResponse<List<TrackerInstallVehicleDto>>> Handle(
        GetTrackerInstallVehiclesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        if (request.TrackerId is > 0)
        {
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                TrackerTenantSql.DeviceExistsForTenant,
                new { Id = request.TrackerId.Value, TenantId = tenantId },
                cancellationToken: cancellationToken));

            if (!exists)
                return ApiResponse<List<TrackerInstallVehicleDto>>.FailResponse("Tracker not found.");
        }

        var rows = await connection.QueryAsync<TrackerInstallVehicleDto>(new CommandDefinition(
            """
            SELECT
                v.Id AS VehicleId,
                v.Name,
                v.RegistrationNumber AS PlateNumber,
                v.VehicleCode,
                CASE
                    WHEN v.Status = 5 THEN CAST(0 AS BIT)
                    WHEN v.Status = 4 THEN CAST(0 AS BIT)
                    WHEN activeTracker.GpsDeviceId IS NULL THEN CAST(1 AS BIT)
                    WHEN @TrackerId IS NOT NULL AND activeTracker.GpsDeviceId = @TrackerId THEN CAST(0 AS BIT)
                    ELSE CAST(0 AS BIT)
                END AS IsSelectable,
                activeTracker.TrackerName AS AssignedTrackerName,
                CASE
                    WHEN v.Status = 5 THEN 'Publish this vehicle before installing a tracker'
                    WHEN v.Status = 4 THEN 'Vehicle is retired'
                    WHEN activeTracker.GpsDeviceId IS NULL THEN NULL
                    WHEN @TrackerId IS NOT NULL AND activeTracker.GpsDeviceId = @TrackerId
                        THEN 'Currently installed on this tracker'
                    WHEN activeTracker.TrackerName IS NOT NULL THEN CONCAT('Already assigned to ', activeTracker.TrackerName)
                    ELSE 'Already has an active tracker'
                END AS BlockedReason
            FROM Vehicles v
            OUTER APPLY (
                SELECT TOP 1 d.Id AS GpsDeviceId, d.Name AS TrackerName
                FROM GpsDevices d
                WHERE d.IsDeleted = 0
                  AND (d.TenantId = @TenantId OR d.TenantId IS NULL)
                  AND (
                      EXISTS (
                          SELECT 1 FROM GpsDeviceAssignments aa
                          WHERE aa.GpsDeviceId = d.Id AND aa.VehicleId = v.Id
                            AND aa.IsActive = 1 AND aa.TenantId = @TenantId
                      )
                      OR d.VehicleId = v.Id
                      OR d.Id = v.GpsDeviceId
                  )
                ORDER BY
                    CASE WHEN EXISTS (
                        SELECT 1 FROM GpsDeviceAssignments aa
                        WHERE aa.GpsDeviceId = d.Id AND aa.VehicleId = v.Id
                          AND aa.IsActive = 1 AND aa.TenantId = @TenantId
                    ) THEN 0 ELSE 1 END,
                    d.UpdatedAt DESC
            ) activeTracker
            WHERE v.TenantId = @TenantId
              AND v.IsDeleted = 0
            ORDER BY IsSelectable DESC, v.Name
            """,
            new { TenantId = tenantId, TrackerId = request.TrackerId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<TrackerInstallVehicleDto>>.SuccessResponse(rows.ToList());
    }
}

internal static class TrackerSql
{
    internal const string ListQuery = """
        SELECT d.Id, d.VehicleId,
               CASE WHEN v.Status = 5 THEN NULL ELSE v.Name END AS VehicleName,
               CASE WHEN v.Status = 5 OR v.RegistrationNumber LIKE 'DRAFT-%' THEN NULL
                    ELSE v.RegistrationNumber END AS PlateNumber,
               d.DriverId,
               dr.FullName AS DriverName,
               d.UniqueId, d.Name, d.Category, d.Phone, d.Contact, d.Disabled,
               d.Protocol, d.TrackerModelKey, d.TrackerModelId,
               b.Id AS TrackerBrandId, b.Name AS TrackerBrandName, m.Name AS ModelName,
               d.Model, d.Vendor,
               d.SupportsEngineCutoff, d.RelayOutput, d.RelayPurpose, d.LastIgnition, d.LastSeenAt, d.IsActive,
               CASE WHEN d.LastSeenAt IS NOT NULL AND d.LastSeenAt > DATEADD(minute, -30, GETUTCDATE())
                    THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsOnline,
               COALESCE(d.LastSpeed, vcl.Speed) AS LastSpeed,
               d.LastBatteryLevel, d.LastRssi,
               d.TraccarDeviceId,
               CASE WHEN d.TraccarDeviceId IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsTraccarLinked,
               CASE WHEN d.UniqueId NOT LIKE '%[^0-9]%' AND LEN(d.UniqueId) = 15
                    THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsValidImei,
               d.SerialNumber, d.InstallationDate, d.InstalledBy, d.InstallationNotes,
               d.CountryCode, d.SIMProvider, d.SIMPackage, d.MonthlySIMCost,
               d.WarrantyStart, d.WarrantyEnd, d.PurchaseDate, d.PurchasePrice,
               d.CurrentStatus, d.LastSyncAt, d.SimNumber
        FROM GpsDevices d
        LEFT JOIN Vehicles v ON v.Id = d.VehicleId AND v.IsDeleted = 0
        LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = d.VehicleId
        LEFT JOIN Drivers dr ON dr.Id = d.DriverId AND dr.IsDeleted = 0
        LEFT JOIN TrackerModels m ON m.Id = d.TrackerModelId
        LEFT JOIN TrackerBrands b ON b.Id = m.TrackerBrandId
        WHERE d.IsDeleted = 0
        """;
}
