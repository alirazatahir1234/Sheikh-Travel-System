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
    DateTime StartAt,
    DateTime? EndAt,
    string? Reason,
    string? Notes,
    string? CreatedBy,
    DateTime CreatedAt);

public record AssignmentStatsDto(
    int TotalAssignments,
    int ActiveAssignments,
    int CompletedAssignments,
    int CancelledAssignments,
    int UnassignedVehicles,
    int AvailableDrivers,
    int ExpiringLicenses);

public record CreateAssignmentRequest(
    int VehicleId,
    int DriverId,
    string AssignmentType,
    DateTime StartDate,
    DateTime? EndDate,
    string? Reason,
    string? Notes);

public record TransferAssignmentRequest(
    int NewVehicleId,
    string? Reason,
    string? Notes);

public record CompleteAssignmentRequest(
    string? Reason);

public record CancelAssignmentRequest(
    string? Reason);

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
