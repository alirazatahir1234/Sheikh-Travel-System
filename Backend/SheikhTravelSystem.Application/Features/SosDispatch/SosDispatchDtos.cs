namespace SheikhTravelSystem.Application.Features.SosDispatch;

public record SosDispatchPlan(
    decimal? SpeedKmh,
    string? LastRouteJson,
    string? LastRouteSummary,
    string RegionCode,
    int? AssignedResponderDriverId,
    int? AssignedResponderUserId,
    string? AssignedTeamName,
    string DispatchMode,
    decimal? DistanceKm,
    int? EtaMinutes,
    int? SuggestedPartnerId,
    string? SuggestedPartnerName,
    string ConfirmationMessage);

public record SosRecoveryPartnerDto(
    int Id,
    string RegionCode,
    string PartnerType,
    string Name,
    string Phone,
    string? Email,
    string? Notes,
    bool IsActive);

public record UpsertSosRecoveryPartnerRequest(
    string RegionCode,
    string PartnerType,
    string Name,
    string Phone,
    string? Email,
    string? Notes,
    bool IsActive);

public record SosEventListItemDto(
    int Id,
    DateTime CreatedAt,
    string Status,
    int DriverId,
    string DriverName,
    string? DriverPhone,
    int? VehicleId,
    double? Latitude,
    double? Longitude,
    decimal? SpeedKmh,
    string? LastRouteSummary,
    string? RegionCode,
    string? DispatchMode,
    int? AssignedResponderDriverId,
    string? AssignedTeamName,
    decimal? DistanceKm,
    int? EtaMinutes,
    string? ConfirmationMessage,
    int? SuggestedPartnerId,
    string? SuggestedPartnerName,
    string? Message);

public record DriverSosStatusDto(
    int Id,
    string Status,
    string? DispatchMode,
    string? AssignedTeamName,
    int? EtaMinutes,
    string? ConfirmationMessage,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

public record VerifiedTeamCandidateRow(
    int DriverId,
    int? UserId,
    string FullName,
    string? Phone,
    double? Latitude,
    double? Longitude);
