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

    public async Task<VehicleFuelTelemetryDto?> GetVehicleTelemetryAsync(
        int tenantId, int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<(string? Name, DateTime? LastUpdate,
            decimal? FuelLevel, decimal? FuelConsumptionRateLph, decimal? ObdFuelUsedLiters, decimal? ObdOdometerKm)>(
            new CommandDefinition("""
                SELECT v.Name,
                       vcl.LastUpdate,
                       vcl.FuelLevel,
                       vcl.FuelConsumptionRateLph,
                       vcl.ObdFuelUsedLiters,
                       vcl.ObdOdometerKm
                FROM Vehicles v
                LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                WHERE v.Id = @VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
                """,
                new { VehicleId = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (row.Name is null)
            return null;

        var hasObd = row.FuelLevel.HasValue || row.FuelConsumptionRateLph.HasValue
            || row.ObdFuelUsedLiters.HasValue || row.ObdOdometerKm.HasValue;

        return new VehicleFuelTelemetryDto(
            vehicleId,
            row.Name,
            row.LastUpdate,
            row.FuelLevel,
            row.FuelConsumptionRateLph,
            row.ObdFuelUsedLiters,
            row.ObdOdometerKm,
            hasObd
                ? "Values come from the linked GPS/OBD device (Traccar attributes: fuel, fuelConsumption, fuelUsed, obdOdometer)."
                : "No OBD fuel telemetry yet — connect an OBD-capable tracker and ensure Traccar forwards fuel PIDs.");
    }

    public async Task<FuelEfficiencyReportDto> GetEfficiencyReportAsync(
        int tenantId, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new { TenantId = tenantId, From = from, To = toExclusive };

        const string odometerBasis =
            "Odometer deltas between consecutive fuel logs (same vehicle); km/L = distance ÷ liters.";

        var vehicleRows = (await connection.QueryAsync<(int VehicleId, string VehicleName, decimal Liters, decimal? DistanceKm)>(
            new CommandDefinition("""
                WITH ordered AS (
                    SELECT f.VehicleId, v.Name AS VehicleName, f.Liters, f.OdometerReading, f.FuelDate,
                           LAG(f.OdometerReading) OVER (PARTITION BY f.VehicleId ORDER BY f.FuelDate, f.Id) AS PrevOdometer
                    FROM FuelLogs f
                    INNER JOIN Vehicles v ON v.Id = f.VehicleId AND v.TenantId = f.TenantId AND v.IsDeleted = 0
                    WHERE f.TenantId = @TenantId AND f.IsDeleted = 0
                      AND f.FuelDate >= @From AND f.FuelDate < @To
                )
                SELECT VehicleId, VehicleName,
                       SUM(Liters) AS Liters,
                       SUM(CASE WHEN PrevOdometer IS NOT NULL AND OdometerReading > PrevOdometer
                           THEN OdometerReading - PrevOdometer ELSE 0 END) AS DistanceKm
                FROM ordered
                GROUP BY VehicleId, VehicleName
                HAVING SUM(Liters) > 0
                ORDER BY VehicleName
                """, p, cancellationToken: cancellationToken))).ToList();

        var driverRows = (await connection.QueryAsync<(int? DriverId, string? DriverName, decimal Liters, decimal? DistanceKm)>(
            new CommandDefinition("""
                WITH ordered AS (
                    SELECT f.DriverId,
                           COALESCE(NULLIF(LTRIM(RTRIM(d.FullName)), ''),
                                    NULLIF(LTRIM(RTRIM(CONCAT(ISNULL(d.FirstName, ''), ' ', ISNULL(d.LastName, '')))), '')) AS DriverName,
                           f.Liters, f.OdometerReading, f.VehicleId, f.FuelDate,
                           LAG(f.OdometerReading) OVER (PARTITION BY f.VehicleId ORDER BY f.FuelDate, f.Id) AS PrevOdometer
                    FROM FuelLogs f
                    LEFT JOIN Drivers d ON d.Id = f.DriverId AND d.TenantId = f.TenantId AND d.IsDeleted = 0
                    WHERE f.TenantId = @TenantId AND f.IsDeleted = 0
                      AND f.FuelDate >= @From AND f.FuelDate < @To
                )
                SELECT DriverId, MAX(DriverName) AS DriverName,
                       SUM(Liters) AS Liters,
                       SUM(CASE WHEN PrevOdometer IS NOT NULL AND OdometerReading > PrevOdometer
                           THEN OdometerReading - PrevOdometer ELSE 0 END) AS DistanceKm
                FROM ordered
                WHERE DriverId IS NOT NULL
                GROUP BY DriverId
                HAVING SUM(Liters) > 0
                ORDER BY DriverName
                """, p, cancellationToken: cancellationToken))).ToList();

        var routeRows = (await connection.QueryAsync<(int RouteId, string RouteName, decimal? DistanceKm, decimal Liters)>(
            new CommandDefinition("""
                SELECT t.RouteId,
                       COALESCE(NULLIF(r.Name, ''), r.Source + N' → ' + r.Destination) AS RouteName,
                       SUM(COALESCE(t.ActualDistanceKm, t.PlannedDistanceKm, r.Distance, 0)) AS DistanceKm,
                       SUM(ISNULL(fuel.Liters, 0)) AS Liters
                FROM Trips t
                INNER JOIN Routes r ON r.Id = t.RouteId AND r.TenantId = t.TenantId AND r.IsDeleted = 0
                OUTER APPLY (
                    SELECT SUM(fl.Liters) AS Liters
                    FROM FuelLogs fl
                    WHERE fl.TenantId = t.TenantId AND fl.IsDeleted = 0
                      AND fl.VehicleId = t.VehicleId
                      AND fl.FuelDate >= @From AND fl.FuelDate < @To
                      AND CAST(fl.FuelDate AS DATE) = CAST(COALESCE(t.ActualStart, t.PlannedStart, t.TripDate) AS DATE)
                ) fuel
                WHERE t.TenantId = @TenantId AND t.IsDeleted = 0 AND t.RouteId IS NOT NULL
                  AND COALESCE(t.ActualStart, t.PlannedStart, t.TripDate) >= @From
                  AND COALESCE(t.ActualStart, t.PlannedStart, t.TripDate) < @To
                GROUP BY t.RouteId, r.Name, r.Source, r.Destination
                HAVING SUM(COALESCE(t.ActualDistanceKm, t.PlannedDistanceKm, r.Distance, 0)) > 0
                ORDER BY RouteName
                """, p, cancellationToken: cancellationToken))).ToList();

        static FuelEfficiencyRowDto Map(string key, string label, decimal? distanceKm, decimal liters, string note)
        {
            decimal? kmPerLiter = distanceKm is > 0 && liters > 0
                ? Math.Round(distanceKm.Value / liters, 2)
                : null;
            return new(key, label, distanceKm > 0 ? distanceKm : null, liters, kmPerLiter, note);
        }

        return new FuelEfficiencyReportDto(
            from,
            toExclusive,
            vehicleRows.Select(r => Map(r.VehicleId.ToString(), r.VehicleName, r.DistanceKm, r.Liters, odometerBasis)).ToList(),
            driverRows.Select(r => Map(r.DriverId!.Value.ToString(), r.DriverName ?? $"Driver #{r.DriverId}", r.DistanceKm, r.Liters, odometerBasis)).ToList(),
            routeRows.Select(r => Map(
                r.RouteId.ToString(),
                r.RouteName,
                r.DistanceKm,
                r.Liters,
                "Trip distance (actual/planned/route catalog) vs fuel logs on the same calendar day as each trip.")).ToList());
    }
}
