using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Queries;

public record GetTrackersQuery : IRequest<ApiResponse<List<TrackerDetailDto>>>;

public class GetTrackersQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetTrackersQuery, ApiResponse<List<TrackerDetailDto>>>
{
    public Task<ApiResponse<List<TrackerDetailDto>>> Handle(GetTrackersQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetTrackersAsync(request, cancellationToken);
}

public record GetTrackerByIdQuery(int Id) : IRequest<ApiResponse<TrackerDetailDto>>;

public class GetTrackerByIdQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetTrackerByIdQuery, ApiResponse<TrackerDetailDto>>
{
    public Task<ApiResponse<TrackerDetailDto>> Handle(GetTrackerByIdQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetTrackerByIdAsync(request, cancellationToken);
}

public record GetTrackerAssignmentsQuery(int TrackerId) : IRequest<ApiResponse<List<TrackerAssignmentDto>>>;

public class GetTrackerAssignmentsQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetTrackerAssignmentsQuery, ApiResponse<List<TrackerAssignmentDto>>>
{
    public Task<ApiResponse<List<TrackerAssignmentDto>>> Handle(GetTrackerAssignmentsQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetTrackerAssignmentsAsync(request, cancellationToken);
}

public record GetTrackerInstallVehiclesQuery(int? TrackerId = null) : IRequest<ApiResponse<List<TrackerInstallVehicleDto>>>;

public class GetTrackerInstallVehiclesQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetTrackerInstallVehiclesQuery, ApiResponse<List<TrackerInstallVehicleDto>>>
{
    public Task<ApiResponse<List<TrackerInstallVehicleDto>>> Handle(GetTrackerInstallVehiclesQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetTrackerInstallVehiclesAsync(request, cancellationToken);
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
