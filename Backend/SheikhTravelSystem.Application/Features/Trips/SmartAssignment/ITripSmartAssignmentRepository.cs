namespace SheikhTravelSystem.Application.Features.Trips.SmartAssignment;

public interface ITripSmartAssignmentRepository
{
    Task<TripSmartAssignmentTripContext?> GetTripContextAsync(int tenantId, int tripId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripAssignmentCandidateRow>> GetCandidatesAsync(
        int tenantId,
        IReadOnlyCollection<int> excludedDriverIds,
        int? forTripId = null,
        DateTime? plannedStartUtc = null,
        CancellationToken cancellationToken = default);

    Task<long> InsertDecisionAsync(
        int tenantId,
        TripSmartAssignmentDecisionWrite decision,
        CancellationToken cancellationToken = default);

    Task MarkPriorProposedSupersededAsync(int tenantId, int tripId, CancellationToken cancellationToken = default);

    Task UpdateDecisionOutcomeAsync(
        int tenantId,
        long decisionId,
        string outcome,
        string? rejectionReason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripSmartAssignmentDecisionDto>> GetDecisionsForTripAsync(
        int tenantId,
        int tripId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripSmartAssignmentDecisionDto>> GetRecentDecisionsAsync(
        int tenantId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetRejectedDriverIdsAsync(
        int tenantId,
        int tripId,
        CancellationToken cancellationToken = default);

    Task<TripSmartAssignmentDecisionDto?> GetLatestActiveDecisionAsync(
        int tenantId,
        int tripId,
        int driverId,
        CancellationToken cancellationToken = default);

    /// <summary>True when a Shadow outcome was already logged today for this trip's top suggestion.</summary>
    Task<bool> HasShadowDecisionTodayAsync(
        int tenantId,
        int tripId,
        CancellationToken cancellationToken = default);
}

public sealed record TripAssignmentCandidateRow(
    int DriverId,
    string DriverName,
    int? UserId,
    int? VehicleId,
    string? VehicleName,
    double? Latitude,
    double? Longitude,
    decimal? FuelLevelPercent,
    decimal HoursWorkedToday,
    decimal? Rating,
    bool IsActive,
    int Status,
    string? VerificationStatus,
    DateTime LicenseExpiryDate,
    int? VehicleFuelType,
    decimal? ElectricRangeKm,
    int? SeatingCapacity = null,
    string? VehicleType = null,
    int? VehicleStatus = null,
    bool HasCriticalMaintenance = false,
    bool HasScheduleConflict = false,
    int? PerformanceScore = null,
    int? SafetyScore = null,
    decimal? FuelAverageKmPerLiter = null,
    decimal? RecentKm7d = null);

public sealed record TripSmartAssignmentDecisionWrite(
    int TripId,
    int DriverId,
    int? VehicleId,
    int Rank,
    decimal TotalScore,
    string ScoreBreakdownJson,
    int? RouteId,
    decimal? DistanceKm,
    int? EtaMinutes,
    string TrafficSource,
    string Outcome,
    string? RejectionReason,
    string? CreatedBy);
