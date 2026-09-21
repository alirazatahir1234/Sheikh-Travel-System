using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class FuelLogRepository(IDbConnectionFactory dbFactory) : IFuelLogRepository
{
    public async Task<PagedResult<FuelLogDto>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var logs = await connection.QueryAsync<FuelLogDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, DriverId, Liters, PricePerLiter, TotalCost,
                  OdometerReading, FuelType, FuelDate, Station, CreatedAt, ReceiptUrl
                  FROM FuelLogs WHERE IsDeleted = 0
                  ORDER BY FuelDate DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, PageSize = pageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM FuelLogs WHERE IsDeleted = 0",
                cancellationToken: cancellationToken));

        return new PagedResult<FuelLogDto>
        {
            Items = logs.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FuelLogDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<FuelLogDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, DriverId, Liters, PricePerLiter, TotalCost,
                  OdometerReading, FuelType, FuelDate, Station, CreatedAt
                  FROM FuelLogs WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(CreateFuelLogDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var totalCost = dto.Liters * dto.PricePerLiter;

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO FuelLogs (VehicleId, DriverId, Liters, PricePerLiter, TotalCost,
                  OdometerReading, FuelType, FuelDate, Station, ReceiptUrl, CreatedAt, IsDeleted)
                  VALUES (@VehicleId, @DriverId, @Liters, @PricePerLiter, @TotalCost,
                  @OdometerReading, @FuelType, @FuelDate, @Station, @ReceiptUrl, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.VehicleId, dto.DriverId, dto.Liters, dto.PricePerLiter, TotalCost = totalCost,
                    dto.OdometerReading, FuelType = (int)dto.FuelType, dto.FuelDate,
                    dto.Station, dto.ReceiptUrl, CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));
    }

    public async Task<int> UpdateAsync(int id, CreateFuelLogDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var totalCost = dto.Liters * dto.PricePerLiter;

        return await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE FuelLogs 
                  SET VehicleId = @VehicleId, DriverId = @DriverId, Liters = @Liters,
                      PricePerLiter = @PricePerLiter, TotalCost = @TotalCost,
                      OdometerReading = @OdometerReading, FuelType = @FuelType,
                      FuelDate = @FuelDate, Station = @Station, UpdatedAt = @UpdatedAt
                  WHERE Id = @Id AND IsDeleted = 0",
                new
                {
                    Id = id,
                    dto.VehicleId, dto.DriverId, dto.Liters, dto.PricePerLiter, TotalCost = totalCost,
                    dto.OdometerReading, FuelType = (int)dto.FuelType, dto.FuelDate,
                    dto.Station, UpdatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE FuelLogs SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));
    }
}
