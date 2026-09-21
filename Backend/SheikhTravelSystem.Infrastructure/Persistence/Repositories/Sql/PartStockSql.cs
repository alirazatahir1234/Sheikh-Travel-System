using Dapper;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

internal static class PartStockSql
{
    internal static async Task<int> GetTotalStockAsync(
        System.Data.IDbConnection connection, int tenantId, int partId, CancellationToken ct)
    {
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT ISNULL(SUM(StockQuantity), 0)
            FROM PartInventory
            WHERE PartId = @PartId AND TenantId = @TenantId
            """, new { PartId = partId, TenantId = tenantId }, cancellationToken: ct));
    }

    internal static async Task InsertMovementAsync(
        System.Data.IDbConnection connection, int tenantId, int partId, string movementType,
        int quantity, string? fromLocation, string? toLocation, int? vehicleId, int? workOrderId,
        string? notes, string createdBy, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO PartStockMovements
                (TenantId, PartId, MovementType, Quantity, FromLocation, ToLocation, VehicleId, WorkOrderId, Notes, CreatedBy)
            VALUES
                (@TenantId, @PartId, @MovementType, @Quantity, @FromLocation, @ToLocation, @VehicleId, @WorkOrderId, @Notes, @CreatedBy)
            """, new
        {
            TenantId = tenantId,
            PartId = partId,
            MovementType = movementType,
            Quantity = quantity,
            FromLocation = fromLocation,
            ToLocation = toLocation,
            VehicleId = vehicleId,
            WorkOrderId = workOrderId,
            Notes = notes,
            CreatedBy = createdBy
        }, cancellationToken: ct));
    }

    internal static async Task MaybeInsertLowStockAlertAsync(
        System.Data.IDbConnection connection, int tenantId, int partId, string partName,
        int stock, int minStock, CancellationToken ct)
    {
        if (stock >= minStock) return;

        await MaintenanceAlertHelper.InsertAlertAsync(
            connection, tenantId, null, "LowStock", "Warning",
            $"Low stock: {partName}", $"Part {partName} stock ({stock}) is below minimum ({minStock}).",
            "Part", partId, ct);
    }
}
