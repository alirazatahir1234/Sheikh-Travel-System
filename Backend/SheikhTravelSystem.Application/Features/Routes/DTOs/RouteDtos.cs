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
    DateTime CreatedAt);

public record CreateRouteDto(
    string? Name,
    string Source,
    string Destination,
    decimal Distance,
    int? EstimatedMinutes,
    decimal BasePrice);

public record UpdateRouteDto(
    string? Name,
    string Source,
    string Destination,
    decimal Distance,
    int? EstimatedMinutes,
    decimal BasePrice,
    bool IsActive);
