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

    Task<bool> RegistrationExistsAsync(
        int tenantId,
        string registrationNumber,
        CancellationToken cancellationToken = default);

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

/// <summary>
/// SQL snapshot for vehicle GPS tab (before optional live Traccar overlay).
/// Mutable class (not positional record): Dapper cannot materialize nullable
/// record constructors when SQL returns non-null column types (e.g. int vs int?).
/// </summary>
public sealed class VehicleGpsSnapshot
{
    public int? GpsDeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? UniqueId { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool? LastIgnition { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public decimal? Speed { get; set; }
    public DateTime? LastUpdate { get; set; }
    public string? SimNumber { get; set; }
    public string? ModelName { get; set; }
    public string? BrandName { get; set; }
    public DateTime? InstallationDate { get; set; }
    public decimal? TotalDistanceKm { get; set; }
    public decimal? BatteryLevel { get; set; }
    public int? GsmSignal { get; set; }
    public string? Address { get; set; }
    public bool GpsOnline { get; set; }
    public decimal? Heading { get; set; }
    public decimal? FuelLevel { get; set; }
    public int? TraccarDeviceId { get; set; }
}
