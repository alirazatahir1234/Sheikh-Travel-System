using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class DriverAllowanceRepository(IDbConnectionFactory dbFactory) : IDriverAllowanceRepository
{
    public async Task<PagedResult<DriverAllowanceRuleDto>> GetPagedAsync(
        int page,
        int pageSize,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var filter = activeOnly ? "AND IsActive = 1" : string.Empty;

        var rules = await connection.QueryAsync<DriverAllowanceRuleDto>(
            new CommandDefinition(
                $@"SELECT Id, Name, CalculationType, Value, Priority,
                          MinDistanceKm, MaxDistanceKm, VehicleFuelType, RouteFilter,
                          IsActive, Notes, CreatedAt
                   FROM DriverAllowanceRules
                   WHERE IsDeleted = 0 {filter}
                   ORDER BY Priority ASC, Id ASC
                   OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, PageSize = pageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT COUNT(*) FROM DriverAllowanceRules WHERE IsDeleted = 0 {filter}",
                cancellationToken: cancellationToken));

        return new PagedResult<DriverAllowanceRuleDto>
        {
            Items = rules.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<DriverAllowanceRuleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<DriverAllowanceRuleDto>(
            new CommandDefinition(
                @"SELECT Id, Name, CalculationType, Value, Priority,
                         MinDistanceKm, MaxDistanceKm, VehicleFuelType, RouteFilter,
                         IsActive, Notes, CreatedAt
                  FROM DriverAllowanceRules WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(CreateDriverAllowanceRuleDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO DriverAllowanceRules
                    (Name, CalculationType, Value, Priority, MinDistanceKm, MaxDistanceKm,
                     VehicleFuelType, RouteFilter, IsActive, Notes, CreatedAt, CreatedBy, IsDeleted)
                  VALUES
                    (@Name, @CalculationType, @Value, @Priority, @MinDistanceKm, @MaxDistanceKm,
                     @VehicleFuelType, @RouteFilter, 1, @Notes, @CreatedAt, @CreatedBy, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.Name,
                    CalculationType = (int)dto.CalculationType,
                    dto.Value,
                    dto.Priority,
                    dto.MinDistanceKm,
                    dto.MaxDistanceKm,
                    VehicleFuelType = dto.VehicleFuelType.HasValue ? (int?)dto.VehicleFuelType : null,
                    dto.RouteFilter,
                    dto.Notes,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "api"
                },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM DriverAllowanceRules WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(int id, UpdateDriverAllowanceRuleDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE DriverAllowanceRules
                    SET Name = @Name, CalculationType = @CalculationType, Value = @Value,
                        Priority = @Priority, MinDistanceKm = @MinDistanceKm,
                        MaxDistanceKm = @MaxDistanceKm, VehicleFuelType = @VehicleFuelType,
                        RouteFilter = @RouteFilter, IsActive = @IsActive, Notes = @Notes,
                        UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
                  WHERE Id = @Id",
                new
                {
                    dto.Name,
                    CalculationType = (int)dto.CalculationType,
                    dto.Value,
                    dto.Priority,
                    dto.MinDistanceKm,
                    dto.MaxDistanceKm,
                    VehicleFuelType = dto.VehicleFuelType.HasValue ? (int?)dto.VehicleFuelType : null,
                    dto.RouteFilter,
                    dto.IsActive,
                    dto.Notes,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "api",
                    Id = id
                },
                cancellationToken: cancellationToken));
    }

    public async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE DriverAllowanceRules SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { UpdatedAt = DateTime.UtcNow, Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<DriverAllowanceRouteContext?> GetRouteContextAsync(int routeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<DriverAllowanceRouteContext>(
            new CommandDefinition(
                "SELECT Distance, Name, Source, Destination FROM Routes WHERE Id = @Id AND IsDeleted = 0",
                new { Id = routeId },
                cancellationToken: cancellationToken));
    }

    public async Task<int?> GetVehicleFuelTypeAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT FuelType FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = vehicleId },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DriverAllowanceRuleDto>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var rules = await connection.QueryAsync<DriverAllowanceRuleDto>(
            new CommandDefinition(
                @"SELECT Id, Name, CalculationType, Value, Priority,
                         MinDistanceKm, MaxDistanceKm, VehicleFuelType, RouteFilter,
                         IsActive, Notes, CreatedAt
                  FROM DriverAllowanceRules
                  WHERE IsDeleted = 0 AND IsActive = 1
                  ORDER BY Priority ASC, Id ASC",
                cancellationToken: cancellationToken));

        return rules.ToList();
    }
}
