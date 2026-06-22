namespace SheikhTravelSystem.Application.Features.Assignments;

public record AssignmentListItemDto(
    int Id,
    string AssignmentNo,
    int VehicleId,
    string VehicleName,
    string? VehicleRegistration,
    string? VehicleCode,
    int? DriverId,
    string? DriverName,
    string? DriverCode,
    string AssignmentType,
    string Status,
    string DisplayStatus,
    DateTime StartAt,
    DateTime? EndAt,
    string? Purpose,
    string? PickupLocation,
    string? DropLocation,
    int? DurationDays,
    decimal? OdometerStart,
    decimal? OdometerEnd,
    string? Reason,
    string? Notes,
    string? CreatedBy,
    DateTime CreatedAt,
    string? ModifiedBy,
    DateTime? ModifiedAt,
    bool GpsOnline,
    decimal? GpsSpeed,
    DateTime? GpsLastSeen,
    bool? Ignition,
    bool DriverLicenseExpiringSoon,
    bool VehicleMaintenanceDue);

public record AssignmentStatsDto(
    int TotalAssignments,
    int ActiveAssignments,
    int CompletedAssignments,
    int CancelledAssignments,
    int UnassignedVehicles,
    int AvailableVehicles,
    int AvailableDrivers,
    int ExpiringLicenses,
    int OngoingTrips,
    int UpcomingAssignments,
    int OverdueReturns,
    int ExpiredDocuments,
    int DriversOnLeave,
    int VehiclesUnderMaintenance,
    decimal AssignmentUtilizationPct);

public record CreateAssignmentRequest(
    int VehicleId,
    int DriverId,
    string AssignmentType,
    DateTime StartDate,
    DateTime? EndDate,
    string? Purpose,
    string? PickupLocation,
    string? DropLocation,
    decimal? OdometerStart,
    string? Reason,
    string? Notes,
    int? BookingId = null);

public record TransferAssignmentRequest(
    string TransferType,
    int? NewVehicleId,
    int? NewDriverId,
    string? Reason,
    string? Notes);

public record CompleteAssignmentRequest(
    string? Reason,
    decimal? OdometerEnd = null);

public record CancelAssignmentRequest(
    string? Reason);

public record ValidateAssignmentRequest(
    int VehicleId,
    int DriverId,
    DateTime? StartDate = null,
    string? AssignmentType = null,
    bool SkipSoftWarnings = false);

public record AssignmentValidationIssueDto(
    string Code,
    string Message,
    string Severity);

public record AssignmentValidationResultDto(
    bool CanProceed,
    IReadOnlyList<AssignmentValidationIssueDto> Issues);

public record AssignmentChangelogDto(
    int Id,
    string ActionType,
    int? OldVehicleId,
    string? OldVehicleName,
    int? NewVehicleId,
    string? NewVehicleName,
    int? OldDriverId,
    string? OldDriverName,
    int? NewDriverId,
    string? NewDriverName,
    string? Reason,
    string? CreatedBy,
    DateTime CreatedAt);

public record ApproveAssignmentRequest(string? Notes);

public record RejectAssignmentRequest(string Reason);

public record BulkAssignmentIdsRequest(IReadOnlyList<int> AssignmentIds, string? Reason);

public record BulkAssignmentResultDto(int Succeeded, int Failed, IReadOnlyList<string> Errors);

public record AssignmentCalendarItemDto(
    int Id,
    string AssignmentNo,
    int VehicleId,
    string VehicleName,
    int? DriverId,
    string? DriverName,
    string Status,
    DateTime StartAt,
    DateTime? EndAt,
    string AssignmentType);

public record AssignmentUtilizationReportDto(
    int TotalVehicles,
    int AssignedVehicles,
    decimal UtilizationPct,
    int TotalDrivers,
    int AssignedDrivers,
    decimal DriverUtilizationPct,
    int ActiveAssignments,
    int CompletedThisMonth);
