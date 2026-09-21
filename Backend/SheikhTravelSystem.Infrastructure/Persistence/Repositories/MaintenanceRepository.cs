using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Maintenance.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class MaintenanceRepository(IDbConnectionFactory dbFactory) : IMaintenanceRepository
{
    public async Task<int> CreateAsync(CreateMaintenanceDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Maintenance (VehicleId, Description, Cost, MaintenanceDate, NextDueDate,
                  Status, ServiceProvider, CreatedAt, IsDeleted)
                  VALUES (@VehicleId, @Description, @Cost, @MaintenanceDate, @NextDueDate,
                  @Status, @ServiceProvider, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.VehicleId, dto.Description, dto.Cost, dto.MaintenanceDate,
                    dto.NextDueDate, Status = (int)MaintenanceStatus.Scheduled,
                    dto.ServiceProvider, CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(int id, CreateMaintenanceDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Maintenance 
                  SET VehicleId = @VehicleId, Description = @Description, Cost = @Cost,
                      MaintenanceDate = @MaintenanceDate, NextDueDate = @NextDueDate,
                      ServiceProvider = @ServiceProvider, UpdatedAt = @UpdatedAt
                  WHERE Id = @Id AND IsDeleted = 0",
                new
                {
                    Id = id,
                    dto.VehicleId, dto.Description, dto.Cost, dto.MaintenanceDate,
                    dto.NextDueDate, dto.ServiceProvider, UpdatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        if (rowsAffected == 0)
            throw new NotFoundException("Maintenance", id);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Maintenance SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));

        if (rowsAffected == 0)
            throw new NotFoundException("Maintenance", id);
    }

    public async Task UpdateStatusAsync(int id, MaintenanceStatus status, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Maintenance WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Maintenance", id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Maintenance SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { Status = (int)status, UpdatedAt = DateTime.UtcNow, Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<MaintenanceDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var records = await connection.QueryAsync<MaintenanceDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, Description, Cost, MaintenanceDate, NextDueDate,
                  Status, ServiceProvider, CreatedAt
                  FROM Maintenance WHERE IsDeleted = 0
                  ORDER BY MaintenanceDate DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, PageSize = pageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Maintenance WHERE IsDeleted = 0",
                cancellationToken: cancellationToken));

        return new PagedResult<MaintenanceDto>
        {
            Items = records.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<MaintenanceDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var maintenance = await connection.QuerySingleOrDefaultAsync<MaintenanceDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, Description, Cost, MaintenanceDate, NextDueDate,
                  Status, ServiceProvider, CreatedAt
                  FROM Maintenance WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id },
                cancellationToken: cancellationToken));

        if (maintenance == null)
            throw new NotFoundException("Maintenance", id);

        return maintenance;
    }
}
