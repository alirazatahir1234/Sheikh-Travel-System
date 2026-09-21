using Dapper;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Routes.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class RouteRepository(IDbConnectionFactory dbFactory) : IRouteRepository
{
    public async Task<int> CreateAsync(CreateRouteDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Routes (Name, Source, Destination, Distance, EstimatedMinutes, BasePrice, IsActive, CreatedAt, IsDeleted, WaypointsJson, OptimizeMode)
                  VALUES (@Name, @Source, @Destination, @Distance, @EstimatedMinutes, @BasePrice, 1, @CreatedAt, 0, @WaypointsJson, @OptimizeMode);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.Name, dto.Source, dto.Destination, dto.Distance,
                    dto.EstimatedMinutes, dto.BasePrice,
                    dto.WaypointsJson, dto.OptimizeMode,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(int id, UpdateRouteDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Routes WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Route", id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Routes SET Name = @Name, Source = @Source, Destination = @Destination,
                  Distance = @Distance, EstimatedMinutes = @EstimatedMinutes, BasePrice = @BasePrice,
                  IsActive = @IsActive, UpdatedAt = @UpdatedAt,
                  WaypointsJson = @WaypointsJson, OptimizeMode = @OptimizeMode
                  WHERE Id = @Id",
                new
                {
                    dto.Name, dto.Source, dto.Destination, dto.Distance,
                    dto.EstimatedMinutes, dto.BasePrice, dto.IsActive,
                    dto.WaypointsJson, dto.OptimizeMode,
                    UpdatedAt = DateTime.UtcNow, Id = id
                },
                cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Routes WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Route", id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Routes SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { UpdatedAt = DateTime.UtcNow, Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<RouteDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var route = await connection.QuerySingleOrDefaultAsync<RouteDto>(
            new CommandDefinition(
                @"SELECT Id, Name, Source, Destination, Distance, EstimatedMinutes, BasePrice, IsActive, CreatedAt,
                         WaypointsJson, OptimizeMode
                  FROM Routes WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id },
                cancellationToken: cancellationToken));

        if (route is null)
            throw new NotFoundException("Route", id);

        return route;
    }

    public async Task<RoutePagedResult> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        string? distanceBand,
        string? priceBand,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;
        var (whereClause, parameters) = RouteQueryFilters.Build(
            search,
            isActive,
            distanceBand,
            priceBand);

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var routes = await connection.QueryAsync<RouteDto>(
            new CommandDefinition(
                $@"SELECT Id, Name, Source, Destination, Distance, EstimatedMinutes, BasePrice, IsActive, CreatedAt,
                         WaypointsJson, OptimizeMode
                  FROM Routes
                  {whereClause}
                  ORDER BY CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                parameters,
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT COUNT(*) FROM Routes {whereClause}",
                parameters,
                cancellationToken: cancellationToken));

        return new RoutePagedResult(routes.ToList(), totalCount);
    }

    public async Task<RouteListStatsDto> GetListStatsAsync(
        string? search,
        bool? isActive,
        string? priceBand,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var (whereClause, parameters) = RouteQueryFilters.Build(
            search,
            isActive,
            distanceBand: null,
            priceBand,
            applyDistanceBand: false);

        parameters.Add("ShortKmMax", RouteQueryFilters.ShortKmMax);
        parameters.Add("MediumKmMax", RouteQueryFilters.MediumKmMax);

        return await connection.QuerySingleAsync<RouteListStatsDto>(
            new CommandDefinition(
                $@"SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Distance > 0 AND Distance < @ShortKmMax THEN 1 ELSE 0 END) AS Short,
                    SUM(CASE WHEN Distance >= @ShortKmMax AND Distance <= @MediumKmMax THEN 1 ELSE 0 END) AS Medium,
                    SUM(CASE WHEN Distance > @MediumKmMax THEN 1 ELSE 0 END) AS Long
                  FROM Routes
                  {whereClause}",
                parameters,
                cancellationToken: cancellationToken));
    }
}

internal static class RouteQueryFilters
{
    public const decimal ShortKmMax = 150m;
    public const decimal MediumKmMax = 500m;
    public const decimal BudgetPrice = 5000m;
    public const decimal MidPrice = 15000m;

    public static (string WhereClause, DynamicParameters Parameters) Build(
        string? search,
        bool? isActive,
        string? distanceBand,
        string? priceBand,
        bool applyDistanceBand = true)
    {
        var where = "WHERE IsDeleted = 0";
        var parameters = new DynamicParameters();

        if (isActive.HasValue)
        {
            where += " AND IsActive = @IsActive";
            parameters.Add("IsActive", isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            where += @" AND (
                Name LIKE @SearchPattern OR
                Source LIKE @SearchPattern OR
                Destination LIKE @SearchPattern OR
                CAST(Distance AS NVARCHAR(32)) LIKE @SearchPattern OR
                CAST(BasePrice AS NVARCHAR(32)) LIKE @SearchPattern)";
            parameters.Add("SearchPattern", $"%{search.Trim()}%");
        }

        if (applyDistanceBand && !string.IsNullOrWhiteSpace(distanceBand))
        {
            switch (distanceBand.Trim().ToUpperInvariant())
            {
                case "SHORT":
                    where += " AND Distance > 0 AND Distance < @ShortKmMax";
                    parameters.Add("ShortKmMax", ShortKmMax);
                    break;
                case "MEDIUM":
                    where += " AND Distance >= @ShortKmMax AND Distance <= @MediumKmMax";
                    parameters.Add("ShortKmMax", ShortKmMax);
                    parameters.Add("MediumKmMax", MediumKmMax);
                    break;
                case "LONG":
                    where += " AND Distance > @MediumKmMax";
                    parameters.Add("MediumKmMax", MediumKmMax);
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(priceBand))
        {
            switch (priceBand.Trim().ToUpperInvariant())
            {
                case "BUDGET":
                    where += " AND BasePrice <= @BudgetPrice";
                    parameters.Add("BudgetPrice", BudgetPrice);
                    break;
                case "MID":
                    where += " AND BasePrice > @BudgetPrice AND BasePrice <= @MidPrice";
                    parameters.Add("BudgetPrice", BudgetPrice);
                    parameters.Add("MidPrice", MidPrice);
                    break;
                case "PREMIUM":
                    where += " AND BasePrice > @MidPrice";
                    parameters.Add("MidPrice", MidPrice);
                    break;
            }
        }

        return (where, parameters);
    }
}
