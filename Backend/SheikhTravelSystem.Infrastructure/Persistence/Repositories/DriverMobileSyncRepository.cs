using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class DriverMobileSyncRepository(IDbConnectionFactory dbFactory) : IDriverMobileSyncRepository
{
    public async Task<DriverMobileIdempotencyRow?> GetIdempotencyAsync(
        int tenantId, string clientOpId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DriverMobileIdempotencyRow>(new CommandDefinition("""
            SELECT ClientOpId, OpType, ResponseJson, HttpStatus, CreatedAt
            FROM DriverMobileIdempotency
            WHERE TenantId = @TenantId AND ClientOpId = @ClientOpId
            """, new { TenantId = tenantId, ClientOpId = clientOpId.Trim() }, cancellationToken: ct));
    }

    public async Task SaveIdempotencyAsync(
        int tenantId,
        int driverId,
        string clientOpId,
        string opType,
        string responseJson,
        int httpStatus,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM DriverMobileIdempotency
                WHERE TenantId = @TenantId AND ClientOpId = @ClientOpId)
            INSERT INTO DriverMobileIdempotency
                (TenantId, DriverId, ClientOpId, OpType, ResponseJson, HttpStatus, CreatedAt)
            VALUES
                (@TenantId, @DriverId, @ClientOpId, @OpType, @ResponseJson, @HttpStatus, GETUTCDATE())
            """, new
        {
            TenantId = tenantId,
            DriverId = driverId,
            ClientOpId = clientOpId.Trim(),
            OpType = opType,
            ResponseJson = responseJson,
            HttpStatus = httpStatus,
        }, cancellationToken: ct));
    }

    public async Task<int> InsertVoiceCommandAsync(DriverVoiceCommandInsert row, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO DriverVoiceCommands
                (TenantId, DriverId, ClientOpId, LanguageCode, Transcript, Intent,
                 VehicleId, BookingId, Latitude, Longitude, SosAlertId, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES
                (@TenantId, @DriverId, @ClientOpId, @LanguageCode, @Transcript, @Intent,
                 @VehicleId, @BookingId, @Latitude, @Longitude, @SosAlertId, @CreatedAt, 0)
            """, row, cancellationToken: ct));
    }
}
