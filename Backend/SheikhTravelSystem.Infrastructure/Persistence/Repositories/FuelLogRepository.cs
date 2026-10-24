using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class FuelLogRepository(IDbConnectionFactory dbFactory) : IFuelLogRepository
{
    // Dapper cannot construct FuelLogDto directly: record uses int?/FuelType/string?
    // while SQL columns materialize as int/int/string. Map via a plain row first.
    private sealed class FuelLogRow
    {
        public int Id { get; init; }
        public int VehicleId { get; init; }
        public string? VehicleName { get; init; }
        public string? VehicleRegistration { get; init; }
        public int? DriverId { get; init; }
        public string? DriverName { get; init; }
        public decimal Liters { get; init; }
        public decimal PricePerLiter { get; init; }
        public decimal TotalCost { get; init; }
        public decimal OdometerReading { get; init; }
        public int FuelTypeValue { get; init; }
        public DateTime FuelDate { get; init; }
        public string? Station { get; init; }
        public DateTime CreatedAt { get; init; }
        public string? ReceiptUrl { get; init; }

        public FuelLogDto ToDto()
        {
            var fuelType = Enum.IsDefined(typeof(FuelType), FuelTypeValue)
                ? (FuelType)FuelTypeValue
                : FuelType.Petrol;
            return new(
                Id,
                VehicleId,
                DriverId,
                Liters,
                PricePerLiter,
                TotalCost,
                OdometerReading,
                fuelType,
                FuelDate,
                Station,
                CreatedAt,
                ReceiptUrl,
                VehicleName,
                VehicleRegistration,
                DriverName);
        }
    }

    // Always select ReceiptUrl + alias FuelType→FuelTypeValue. Mapping via FuelLogRow
    // avoids Dapper record ctor failures (int? DriverId / FuelType enum / optional ReceiptUrl).
    private const string FuelLogSelectSql = @"
                  SELECT f.Id, f.VehicleId,
                         v.Name AS VehicleName,
                         v.RegistrationNumber AS VehicleRegistration,
                         f.DriverId,
                         COALESCE(NULLIF(LTRIM(RTRIM(d.FullName)), ''),
                                  NULLIF(LTRIM(RTRIM(CONCAT(ISNULL(d.FirstName, ''), ' ', ISNULL(d.LastName, '')))), '')) AS DriverName,
                         f.Liters, f.PricePerLiter, f.TotalCost,
                         f.OdometerReading, f.FuelType AS FuelTypeValue, f.FuelDate, f.Station,
                         f.CreatedAt, f.ReceiptUrl
                  FROM FuelLogs f
                  LEFT JOIN Vehicles v ON v.Id = f.VehicleId AND v.IsDeleted = 0
                    AND v.TenantId = f.TenantId
                  LEFT JOIN Drivers d ON d.Id = f.DriverId AND d.IsDeleted = 0
                    AND d.TenantId = f.TenantId";

    public async Task<PagedResult<FuelLogDto>> GetPagedAsync(
        int tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var logs = await connection.QueryAsync<FuelLogRow>(
            new CommandDefinition(
                $@"{FuelLogSelectSql}
                  WHERE f.IsDeleted = 0 AND f.TenantId = @TenantId
                  ORDER BY f.FuelDate DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, PageSize = pageSize, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM FuelLogs WHERE IsDeleted = 0 AND TenantId = @TenantId",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));

        return new PagedResult<FuelLogDto>
        {
            Items = logs.Select(r => r.ToDto()).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FuelLogDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<FuelLogRow>(
            new CommandDefinition(
                $@"{FuelLogSelectSql}
                  WHERE f.Id = @Id AND f.TenantId = @TenantId AND f.IsDeleted = 0",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return row?.ToDto();
    }

    public async Task<int> CreateAsync(int tenantId, CreateFuelLogDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var totalCost = dto.Liters * dto.PricePerLiter;

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO FuelLogs (TenantId, VehicleId, DriverId, Liters, PricePerLiter, TotalCost,
                  OdometerReading, FuelType, FuelDate, Station, ReceiptUrl, CreatedAt, IsDeleted)
                  VALUES (@TenantId, @VehicleId, @DriverId, @Liters, @PricePerLiter, @TotalCost,
                  @OdometerReading, @FuelType, @FuelDate, @Station, @ReceiptUrl, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    TenantId = tenantId,
                    dto.VehicleId, dto.DriverId, dto.Liters, dto.PricePerLiter, TotalCost = totalCost,
                    dto.OdometerReading, FuelType = (int)dto.FuelType, dto.FuelDate,
                    dto.Station, dto.ReceiptUrl, CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));
    }

    public async Task<int> UpdateAsync(int id, int tenantId, CreateFuelLogDto dto, CancellationToken cancellationToken = default)
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
                  WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new
                {
                    Id = id,
                    TenantId = tenantId,
                    dto.VehicleId, dto.DriverId, dto.Liters, dto.PricePerLiter, TotalCost = totalCost,
                    dto.OdometerReading, FuelType = (int)dto.FuelType, dto.FuelDate,
                    dto.Station, UpdatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE FuelLogs SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = id, TenantId = tenantId, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));
    }
}
