using SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>Visits report filters. From/To are the company's local calendar days, inclusive; both are required.</summary>
public sealed record GeofenceVisitFilter(
    int? GeofenceId,
    IReadOnlyList<int>? VehicleIds,
    int? DriverId,
    DateOnly? From,
    DateOnly? To,
    string? Status);

/// <summary>
/// The geofences workspace (list, detail, KPIs, visits, groups, types, save, bulk, import). Every method works in the
/// caller's tenant from <see cref="ITenantContext"/>; ids of another tenant read as not found.
/// </summary>
public interface IGeofenceWorkspaceRepository
{
    Task<IReadOnlyList<GeofenceListItemDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<GeofenceDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken = default);

    Task<GeofenceSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>Null when the geofence does not exist in this tenant.</summary>
    Task<IReadOnlyList<GeofenceInsideVehicleDto>?> GetInsideAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The geofence's entries, exits and rule alerts on a local day (today when null); null when not found.</summary>
    Task<IReadOnlyList<GeofenceActivityItemDto>?> GetActivityAsync(int id, DateOnly? day, CancellationToken cancellationToken = default);

    Task<GeofenceVisitsPageDto> GetVisitsAsync(GeofenceVisitFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<GeofenceVisitsSummaryDto> GetVisitsSummaryAsync(GeofenceVisitFilter filter, CancellationToken cancellationToken = default);

    Task<TestGeofenceShapeResultDto> TestShapeAsync(TestGeofenceShapeDto shape, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeofenceGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default);

    Task<ApiResponse<int>> SaveGroupAsync(int? id, SaveGeofenceGroupDto group, CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteGroupAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeofenceTypeDto>> GetTypesAsync(CancellationToken cancellationToken = default);

    Task<ApiResponse<int>> CreateTypeAsync(CreateGeofenceTypeDto type, CancellationToken cancellationToken = default);

    Task<GeofenceRecipientOptionsDto> GetRecipientOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates (id null) or updates a geofence with its assignments, recipients and entry/exit alert rule.</summary>
    Task<ApiResponse<int>> SaveAsync(int? id, SaveGeofenceDto geofence, CancellationToken cancellationToken = default);

    Task<ApiResponse<BulkGeofenceResultDto>> BulkAsync(BulkGeofenceActionDto action, CancellationToken cancellationToken = default);

    Task<ApiResponse<ImportGeofencesResultDto>> ImportAsync(IReadOnlyList<SaveGeofenceDto> items, CancellationToken cancellationToken = default);
}
