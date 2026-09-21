using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Application.Features.Vehicles.Queries;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for vehicles and related documents/tabs. SQL lives in Infrastructure.
/// </summary>
public interface IVehicleRepository
{
    Task<(IReadOnlyList<VehicleListItemDto> Items, int TotalCount)> GetPagedAsync(
        int tenantId,
        int page,
        int pageSize,
        bool includeDrafts,
        DataScopeResult? scope,
        CancellationToken cancellationToken = default);

    Task<VehicleDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task EnsureExistsAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(
        int tenantId,
        CreateVehicleDto dto,
        bool saveAsDraft,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        int id,
        int tenantId,
        UpdateVehicleDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes vehicle and documents; returns file URLs for storage cleanup.</summary>
    Task<IReadOnlyList<string>> SoftDeleteAsync(
        int id,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<VehicleStatus> ToggleStatusAsync(
        int id,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task ChangeStatusAsync(
        int id,
        int tenantId,
        VehicleStatus status,
        CancellationToken cancellationToken = default);

    Task<int> AssignDriverAsync(
        int vehicleId,
        int tenantId,
        AssignVehicleDriverRequest body,
        string createdBy,
        CancellationToken cancellationToken = default);

    Task AssignGpsAsync(
        int vehicleId,
        int tenantId,
        int gpsDeviceId,
        CancellationToken cancellationToken = default);

    Task PublishAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<VehicleMaintenanceDto> Items, int TotalCount)> GetMaintenancePagedAsync(
        int vehicleId,
        int tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<VehicleFuelSummaryDto> GetFuelSummaryAsync(
        int vehicleId,
        int tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<VehicleGpsSnapshot?> GetGpsSnapshotAsync(
        int vehicleId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleDocumentDto>> GetDocumentsAsync(
        int vehicleId,
        int tenantId,
        CancellationToken cancellationToken = default);

    /// <returns>Document id and whether an existing row was updated.</returns>
    Task<(int Id, bool Updated)> UpsertDocumentAsync(
        int vehicleId,
        int tenantId,
        string documentType,
        string? fileUrl,
        DateTime? expiryDate,
        string? notes,
        CancellationToken cancellationToken = default);

    Task SetPrimaryImageAsync(
        int vehicleId,
        int documentId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<int> PersistUploadedDocumentAsync(
        int vehicleId,
        int tenantId,
        string documentType,
        string storageKey,
        DateTime? expiryDate,
        string? notes,
        CancellationToken cancellationToken = default);
}

/// <summary>SQL snapshot for vehicle GPS tab (before optional live Traccar overlay).</summary>
public sealed record VehicleGpsSnapshot(
    int? GpsDeviceId,
    string? DeviceName,
    string? UniqueId,
    bool? IsActive,
    DateTime? LastSeenAt,
    bool? LastIgnition,
    double? Latitude,
    double? Longitude,
    decimal? Speed,
    DateTime? LastUpdate,
    string? SimNumber,
    string? ModelName,
    string? BrandName,
    DateTime? InstallationDate,
    decimal? TotalDistanceKm,
    decimal? BatteryLevel,
    int? GsmSignal,
    string? Address,
    bool GpsOnline,
    decimal? Heading,
    decimal? FuelLevel,
    int? TraccarDeviceId);
