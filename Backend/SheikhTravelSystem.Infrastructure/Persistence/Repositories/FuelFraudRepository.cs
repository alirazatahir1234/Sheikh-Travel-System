using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FuelFraud;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class FuelFraudRepository(IDbConnectionFactory dbFactory) : IFuelFraudRepository
{
    private const string ListSelect = """
        SELECT o.Id, o.TenantId, t.Name AS TenantName,
               o.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehicleRegistration,
               o.DriverId, d.FullName AS DriverName,
               o.OrderedLiters, o.ActualIncreaseLiters, o.ShortfallLiters,
               o.FuelLevelAtOrderPercent, o.FuelLevelAfterPercent,
               o.StationName, o.Latitude, o.Longitude, o.Status, o.GpsAlertEventId,
               o.CreatedAtUtc, o.VerifiedAtUtc, o.ResolvedAtUtc, o.ResolvedBy
        FROM FuelPumpOrders o
        INNER JOIN Tenants t ON t.Id = o.TenantId
        INNER JOIN Vehicles v ON v.Id = o.VehicleId AND v.IsDeleted = 0
        INNER JOIN Drivers d ON d.Id = o.DriverId AND d.IsDeleted = 0
        WHERE o.IsDeleted = 0
        """;

    public async Task<int> InsertPumpOrderAsync(FuelPumpOrderInsertRow row, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO FuelPumpOrders
                (TenantId, VehicleId, DriverId, OrderedLiters, FuelLevelAtOrderPercent, TankCapacityLiters,
                 StationName, Latitude, Longitude, Status)
            OUTPUT INSERTED.Id
            VALUES
                (@TenantId, @VehicleId, @DriverId, @OrderedLiters, @FuelLevelAtOrderPercent, @TankCapacityLiters,
                 @StationName, @Latitude, @Longitude, N'PendingVerification')
            """,
            row,
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<FuelPumpOrderRow>> ListPendingForVehicleAsync(
        int vehicleId,
        int maxAgeMinutes,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<FuelPumpOrderRow>(new CommandDefinition(
            """
            SELECT Id, TenantId, VehicleId, DriverId, OrderedLiters,
                   FuelLevelAtOrderPercent, TankCapacityLiters, FuelLevelAfterPercent,
                   ActualIncreaseLiters, ShortfallLiters, StationName, Latitude, Longitude,
                   Status, GpsAlertEventId, CreatedAtUtc
            FROM FuelPumpOrders
            WHERE VehicleId = @VehicleId AND IsDeleted = 0 AND Status = N'PendingVerification'
              AND CreatedAtUtc >= DATEADD(MINUTE, -@MaxAgeMinutes, GETUTCDATE())
            ORDER BY CreatedAtUtc ASC
            """,
            new { VehicleId = vehicleId, MaxAgeMinutes = maxAgeMinutes },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task CompleteVerificationAsync(FuelPumpOrderVerificationUpdate update, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE FuelPumpOrders SET
                Status = @Status,
                FuelLevelAfterPercent = @FuelLevelAfterPercent,
                ActualIncreaseLiters = @ActualIncreaseLiters,
                ShortfallLiters = @ShortfallLiters,
                GpsAlertEventId = @GpsAlertEventId,
                VerifiedAtUtc = @VerifiedAtUtc
            WHERE Id = @Id AND IsDeleted = 0
            """,
            update,
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<FuelFraudEventListItemDto>> ListEventsAsync(
        int tenantId,
        string? status,
        int limit,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = ListSelect + " AND o.TenantId = @TenantId";
        if (!string.IsNullOrWhiteSpace(status))
            sql += " AND o.Status = @Status";
        sql += " ORDER BY o.CreatedAtUtc DESC OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        var rows = await connection.QueryAsync<FuelFraudEventListItemDto>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, Status = status, Limit = limit },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<FuelFraudEventListItemDto>> ListEventsPlatformAsync(
        int? tenantId,
        string? status,
        int limit,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = ListSelect;
        if (tenantId.HasValue)
            sql += " AND o.TenantId = @TenantId";
        if (!string.IsNullOrWhiteSpace(status))
            sql += " AND o.Status = @Status";
        sql += " ORDER BY CASE WHEN o.Status = N'FraudAlert' THEN 0 ELSE 1 END, o.CreatedAtUtc DESC";
        sql += " OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        var rows = await connection.QueryAsync<FuelFraudEventListItemDto>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, Status = status, Limit = limit },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<bool> ResolveEventAsync(int tenantId, int id, string resolvedBy, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var n = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE FuelPumpOrders SET
                Status = N'Resolved',
                ResolvedAtUtc = GETUTCDATE(),
                ResolvedBy = @ResolvedBy
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new { Id = id, TenantId = tenantId, ResolvedBy = resolvedBy },
            cancellationToken: ct));
        return n > 0;
    }

    public async Task<(decimal? FuelLevel, decimal? TankCapacity, string? VehicleName, string? Registration)?>
        GetVehicleFuelContextAsync(int tenantId, int vehicleId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<VehicleFuelContextRow>(new CommandDefinition(
            """
            SELECT vcl.FuelLevel AS FuelLevel, v.TankCapacityLiters AS TankCapacity,
                   v.Name AS VehicleName, v.RegistrationNumber AS Registration
            FROM Vehicles v
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            WHERE v.Id = @VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            """,
            new { VehicleId = vehicleId, TenantId = tenantId },
            cancellationToken: ct));

        return row?.VehicleName is null ? null : (row.FuelLevel, row.TankCapacity, row.VehicleName, row.Registration);
    }

    private sealed class VehicleFuelContextRow
    {
        public decimal? FuelLevel { get; init; }
        public decimal? TankCapacity { get; init; }
        public string? VehicleName { get; init; }
        public string? Registration { get; init; }
    }
}
