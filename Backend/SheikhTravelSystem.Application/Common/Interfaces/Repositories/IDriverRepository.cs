using System.Data;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for drivers. SQL lives in Infrastructure.
/// </summary>
public interface IDriverRepository
{
    // ── Uniqueness ──────────────────────────────────────────────────────────

    Task EnsureUniqueAsync(
        int tenantId,
        string phone,
        string? email,
        string licenseNumber,
        int? excludeId,
        CancellationToken cancellationToken = default);

    Task<DriverAvailabilityDto> CheckAvailabilityAsync(
        int tenantId,
        string? phone,
        string? email,
        string? licenseNumber,
        int? excludeId,
        CancellationToken cancellationToken = default);

    // ── CRUD ────────────────────────────────────────────────────────────────

    Task<int> CreateAsync(
        int tenantId,
        CreateDriverDto dto,
        string fullName,
        string driverCode,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        int tenantId,
        int id,
        UpdateDriverDto dto,
        string fullName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the driver. Returns prior PhotoUrl (may be null).
    /// Throws <see cref="Exceptions.NotFoundException"/> when missing.
    /// </summary>
    Task<string?> SoftDeleteAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws <see cref="Exceptions.NotFoundException"/> when missing.
    /// </summary>
    Task<DriverDto> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// When scope intersection fails, <see cref="DriverListQueryResult.ScopeError"/> is set
    /// (caller should return FailResponse).
    /// </summary>
    Task<DriverListQueryResult> GetPagedAsync(
        int tenantId,
        int page,
        int pageSize,
        string? q,
        DriverStatus? status,
        int? branchId,
        string? licenseExpiry,
        string? verificationStatus,
        string? availability,
        DataScopeResult? scope,
        CancellationToken cancellationToken = default);

    Task<DriverStatsDto> GetStatsAsync(int tenantId, CancellationToken cancellationToken = default);

    // ── Status ──────────────────────────────────────────────────────────────

    /// <summary>Returns new IsActive value. Throws NotFound when missing.</summary>
    Task<bool> ToggleActiveAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    Task ChangeStatusAsync(int tenantId, int id, DriverStatus status, CancellationToken cancellationToken = default);

    // ── Photo / documents / verification ────────────────────────────────────

    Task UpdatePhotoUrlAsync(int tenantId, int driverId, string photoUrl, CancellationToken cancellationToken = default);

    Task SoftDeleteDocumentsByTypeAsync(
        int tenantId,
        int driverId,
        string documentType,
        CancellationToken cancellationToken = default);

    Task<int> InsertDocumentAsync(
        int tenantId,
        int driverId,
        string documentType,
        string fileUrl,
        DateTime? expiryDate,
        CancellationToken cancellationToken = default);

    Task UpdateVerificationStatusAsync(
        int tenantId,
        int driverId,
        string verificationStatus,
        CancellationToken cancellationToken = default);

    Task UpdateDocumentStatusAsync(
        int tenantId,
        int driverId,
        int documentId,
        string status,
        string? rejectionReason,
        string reviewer,
        CancellationToken cancellationToken = default);

    Task<int> InsertReviewNoteAsync(
        int tenantId,
        int driverId,
        string note,
        string? documentType,
        string createdBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverDocumentDetailedDto>> GetDocumentsAsync(
        int tenantId,
        int driverId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverReviewNoteDto>> GetReviewNotesAsync(
        int tenantId,
        int driverId,
        CancellationToken cancellationToken = default);

    // ── Assignment ──────────────────────────────────────────────────────────

    Task<(string VerificationStatus, DateTime LicenseExpiry, bool IsActive, int Status)?> GetAssignmentGuardRowAsync(
        int tenantId,
        int driverId,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task EnsureDriverNotOnActiveTripAsync(
        int tenantId,
        int driverId,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task EnsureVehicleAvailableForDriverAsync(
        int tenantId,
        int vehicleId,
        int driverId,
        int? excludeAssignmentId,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task EnsureVehicleAssignableAsync(
        int tenantId,
        int vehicleId,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<int> CompleteActiveAssignmentsAsync(
        int tenantId,
        int driverId,
        int? vehicleId,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> VehicleExistsAsync(
        int tenantId,
        int vehicleId,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<int> InsertAssignmentAsync(
        int tenantId,
        int driverId,
        int vehicleId,
        int? bookingId,
        string assignmentType,
        DateTime startAt,
        string? notes,
        string createdBy,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns number of assignments completed. Throws NotFound when driver missing.</summary>
    Task<int> UnassignVehicleAsync(int tenantId, int driverId, CancellationToken cancellationToken = default);

    Task<int> AssignVehicleAsync(
        int tenantId,
        int driverId,
        AssignDriverVehicleRequest body,
        string createdBy,
        CancellationToken cancellationToken = default);

    // ── Extended queries ────────────────────────────────────────────────────

    Task<IReadOnlyList<DriverTimelineEventDto>> GetTimelineAsync(
        int tenantId,
        int driverId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<DriverTripSummaryRow> RecentTrips, int FuelLogCount, bool HasGpsAssignment)> GetActiveDutyAsync(
        int tenantId,
        int driverId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<DriverAssignmentDto>> GetAssignmentsAsync(
        int tenantId,
        int driverId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<DriversAvailabilitySummaryDto> GetAvailabilitySummaryAsync(
        int tenantId,
        int? branchId,
        CancellationToken cancellationToken = default);

    Task<DriverAvailabilityDetailDto?> GetAvailabilityDetailAsync(
        int tenantId,
        int driverId,
        CancellationToken cancellationToken = default);

    // ── Performance ─────────────────────────────────────────────────────────

    Task UpdateRatingAsync(int tenantId, int driverId, decimal rating, CancellationToken cancellationToken = default);

    Task<int> CreateViolationAsync(
        int tenantId,
        int driverId,
        CreateDriverViolationRequest body,
        string createdBy,
        CancellationToken cancellationToken = default);

    Task<int> CreateAttendanceAsync(
        int tenantId,
        int driverId,
        CreateDriverAttendanceRequest body,
        CancellationToken cancellationToken = default);

    Task<DriverPerformanceSummaryDto?> GetPerformanceSummaryAsync(
        int tenantId,
        int driverId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<PagedResult<DriverViolationDto>> GetViolationsAsync(
        int tenantId,
        int driverId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverAttendanceDto>> GetAttendanceAsync(
        int tenantId,
        int driverId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<DriverLocationDto?> GetLocationAsync(
        int tenantId,
        int driverId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverLocationPointDto>> GetLocationHistoryAsync(
        int tenantId,
        int driverId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}

public sealed record DriverListQueryResult(
    IReadOnlyList<DriverListItemDto> Items,
    int TotalCount,
    string? ScopeError = null)
{
    public bool IsScopeFailure => ScopeError is not null;
}

public sealed record DriverTripSummaryRow(int Id, string Status, DateTime? TripDate, string? Route);
