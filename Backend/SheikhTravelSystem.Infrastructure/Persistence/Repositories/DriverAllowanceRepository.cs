using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class DriverAllowanceRepository(IDbConnectionFactory dbFactory) : IDriverAllowanceRepository
{
    public async Task<PagedResult<DriverAllowanceRuleDto>> GetPagedAsync(
        int tenantId,
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
                   WHERE IsDeleted = 0 AND TenantId = @TenantId {filter}
                   ORDER BY Priority ASC, Id ASC
                   OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, PageSize = pageSize, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT COUNT(*) FROM DriverAllowanceRules WHERE IsDeleted = 0 AND TenantId = @TenantId {filter}",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));

        return new PagedResult<DriverAllowanceRuleDto>
        {
            Items = rules.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<DriverAllowanceRuleDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<DriverAllowanceRuleDto>(
            new CommandDefinition(
                @"SELECT Id, Name, CalculationType, Value, Priority,
                         MinDistanceKm, MaxDistanceKm, VehicleFuelType, RouteFilter,
                         IsActive, Notes, CreatedAt
                  FROM DriverAllowanceRules WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(int tenantId, CreateDriverAllowanceRuleDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO DriverAllowanceRules
                    (TenantId, Name, CalculationType, Value, Priority, MinDistanceKm, MaxDistanceKm,
                     VehicleFuelType, RouteFilter, IsActive, Notes, CreatedAt, CreatedBy, IsDeleted)
                  VALUES
                    (@TenantId, @Name, @CalculationType, @Value, @Priority, @MinDistanceKm, @MaxDistanceKm,
                     @VehicleFuelType, @RouteFilter, 1, @Notes, @CreatedAt, @CreatedBy, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    TenantId = tenantId,
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

    public async Task<bool> ExistsAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM DriverAllowanceRules WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(int id, int tenantId, UpdateDriverAllowanceRuleDto dto, CancellationToken cancellationToken = default)
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
                  WHERE Id = @Id AND TenantId = @TenantId",
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
                    Id = id,
                    TenantId = tenantId
                },
                cancellationToken: cancellationToken));
    }

    public async Task SoftDeleteAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE DriverAllowanceRules SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { UpdatedAt = DateTime.UtcNow, Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<DriverAllowanceRouteContext?> GetRouteContextAsync(int routeId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<DriverAllowanceRouteContext>(
            new CommandDefinition(
                "SELECT Distance, Name, Source, Destination FROM Routes WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = routeId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<int?> GetVehicleFuelTypeAsync(int vehicleId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT FuelType FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DriverAllowanceRuleDto>> GetActiveRulesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var rules = await connection.QueryAsync<DriverAllowanceRuleDto>(
            new CommandDefinition(
                @"SELECT Id, Name, CalculationType, Value, Priority,
                         MinDistanceKm, MaxDistanceKm, VehicleFuelType, RouteFilter,
                         IsActive, Notes, CreatedAt
                  FROM DriverAllowanceRules
                  WHERE IsDeleted = 0 AND IsActive = 1 AND TenantId = @TenantId
                  ORDER BY Priority ASC, Id ASC",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));

        return rules.ToList();
    }
}
