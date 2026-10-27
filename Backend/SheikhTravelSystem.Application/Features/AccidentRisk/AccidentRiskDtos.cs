namespace SheikhTravelSystem.Application.Features.AccidentRisk;

public record AccidentRiskFactorDto(
    string Key,
    string Label,
    int RawCount,
    int Points,
    string Detail);

public record DriverAccidentRiskDto(
    int DriverId,
    string DriverName,
    int RiskScore,
    string RiskBand,
    bool IsHighRisk,
    IReadOnlyList<AccidentRiskFactorDto> Factors);

public record RouteAccidentRiskDto(
    int RouteId,
    string RouteLabel,
    decimal DistanceKm,
    int TripCountInPeriod,
    int RiskScore,
    string RiskBand,
    bool IsHighRisk,
    IReadOnlyList<AccidentRiskFactorDto> Factors);

public record AccidentRiskDashboardDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int HighRiskDriverCount,
    int HighRiskRouteCount,
    IReadOnlyList<DriverAccidentRiskDto> Drivers,
    IReadOnlyList<RouteAccidentRiskDto> Routes);

public record DriverRiskMetricRow(
    int DriverId,
    string DriverName,
    int AccidentCount,
    int SpeedViolationCount,
    int FatigueEventCount);

public record RouteRiskMetricRow(
    int RouteId,
    string? RouteName,
    string Source,
    string Destination,
    decimal DistanceKm,
    int TripCount,
    int AccidentCount,
    int SpeedViolationCount);
