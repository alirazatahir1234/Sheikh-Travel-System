using SheikhTravelSystem.Application.Features.Routes.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for routes. SQL lives in Infrastructure.
/// </summary>
public interface IRouteRepository
{
    Task<int> CreateAsync(CreateRouteDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws <see cref="Exceptions.NotFoundException"/> when the route is missing.
    /// </summary>
    Task UpdateAsync(int id, UpdateRouteDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the route. Throws <see cref="Exceptions.NotFoundException"/> when missing.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws <see cref="Exceptions.NotFoundException"/> when the route is missing.
    /// </summary>
    Task<RouteDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<RoutePagedResult> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        string? distanceBand,
        string? priceBand,
        CancellationToken cancellationToken = default);

    Task<RouteListStatsDto> GetListStatsAsync(
        string? search,
        bool? isActive,
        string? priceBand,
        CancellationToken cancellationToken = default);
}

public sealed record RoutePagedResult(IReadOnlyList<RouteDto> Items, int TotalCount);
