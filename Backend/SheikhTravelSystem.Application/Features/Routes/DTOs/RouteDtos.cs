namespace SheikhTravelSystem.Application.Features.Routes.DTOs;

public record RouteDto(
    int Id,
    string? Name,
    string Source,
    string Destination,
    decimal Distance,
    int? EstimatedMinutes,
    decimal BasePrice,
    bool IsActive,
    DateTime CreatedAt,
    string? WaypointsJson = null,
    string? OptimizeMode = null);

public record CreateRouteDto(
    string? Name,
    string Source,
    string Destination,
    decimal Distance,
    int? EstimatedMinutes,
    decimal BasePrice,
    string? WaypointsJson = null,
    string? OptimizeMode = null);

public record UpdateRouteDto(
    string? Name,
    string Source,
    string Destination,
    decimal Distance,
    int? EstimatedMinutes,
    decimal BasePrice,
    bool IsActive,
    string? WaypointsJson = null,
    string? OptimizeMode = null);

public record RouteListStatsDto(
    int Total,
    int Short,
    int Medium,
    int Long);
