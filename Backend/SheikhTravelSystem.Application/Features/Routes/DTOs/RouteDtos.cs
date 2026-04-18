namespace SheikhTravelSystem.Application.Features.Routes.DTOs;

public record RouteDto(int Id, string Source, string Destination, decimal Distance, decimal BasePrice, bool IsActive, DateTime CreatedAt);

public record CreateRouteDto(string Source, string Destination, decimal Distance, decimal BasePrice);

public record UpdateRouteDto(string Source, string Destination, decimal Distance, decimal BasePrice, bool IsActive);
