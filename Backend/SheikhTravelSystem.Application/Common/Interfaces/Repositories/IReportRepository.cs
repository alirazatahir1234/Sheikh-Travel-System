using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Reports.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for KPI and fleet tabular reports. SQL lives in Infrastructure.
/// </summary>
public interface IReportRepository
{
    Task<BookingReportDto> GetBookingReportAsync(
        DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<RevenueReportDto> GetRevenueReportAsync(
        DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleProfitDto>> GetVehicleProfitAsync(
        DateTime from, DateTime to, int? vehicleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverPerformanceDto>> GetDriverPerformanceAsync(
        DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<HashSet<int>> ResolveVehicleIdsForDriverAsync(
        int tenantId, int driverId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<HashSet<int>> GetVehicleIdsByBranchDepartmentAsync(
        int tenantId, int? branchId, int? departmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FleetVehicleReportRow>> GetVehicleReportRowsAsync(
        int tenantId,
        int? branchId,
        int? departmentId,
        DataScopeResult? scope,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FleetFuelReportRow>> GetFuelReportRowsAsync(
        int tenantId,
        DateTime from,
        DateTime to,
        int? vehicleId,
        int? branchId,
        DataScopeResult? scope,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FleetDriverProfileRow>> GetDriverProfilesAsync(
        int tenantId,
        int? branchId,
        int? departmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, string>> GetActiveAssignmentVehicleNamesAsync(
        int tenantId,
        IReadOnlyCollection<int> driverIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignmentWindowRow>> GetAssignmentWindowsAsync(
        int tenantId,
        IReadOnlyCollection<int> vehicleIds,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, string>> GetDriverNamesAsync(
        int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, decimal>> GetActiveSpeedLimitsByVehicleAsync(
        int tenantId, CancellationToken cancellationToken = default);

    Task<Dictionary<int, int>> ResolveVehicleToDeviceMapAsync(
        int tenantId,
        IEnumerable<int> vehicleIds,
        CancellationToken cancellationToken = default);

    Task<bool> HasNonTraccarVehicleAsync(
        int tenantId,
        IEnumerable<int> vehicleIds,
        CancellationToken cancellationToken = default);
}

public sealed record FleetVehicleReportRow(
    int Id,
    string Name,
    string? Plate,
    string? FleetNo,
    string? Make,
    string? Model,
    int? Year,
    int FuelType,
    decimal? CurrentMileage,
    DateTime? InsuranceExpiryDate,
    int Status,
    string? DriverName,
    string? TrackerName);

public sealed record FleetFuelReportRow(
    int Id,
    DateTime FuelDate,
    string VehicleName,
    int FuelType,
    decimal Liters,
    decimal PricePerLiter,
    decimal TotalCost,
    decimal? OdometerReading,
    decimal? OdometerDelta);

public sealed record FleetDriverProfileRow(
    int Id,
    string FullName,
    string LicenseNumber,
    string Phone,
    DateTime LicenseExpiryDate);

public record AssignmentWindowRow(
    int VehicleId,
    int? DriverId,
    DateTime StartAt,
    DateTime? EndAt);
