using System.Text.Json;
using Dapper;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public static class PartStockHelper
{
    public const string StatusInStock = "InStock";
    public const string StatusLowStock = "LowStock";
    public const string StatusOutOfStock = "OutOfStock";

    public static string? SerializeCompatibility(IReadOnlyList<string>? items)
    {
        if (items is null || items.Count == 0) return null;
        var cleaned = items.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct().ToList();
        return cleaned.Count == 0 ? null : JsonSerializer.Serialize(cleaned);
    }

    public static IReadOnlyList<string> ParseCompatibility(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return json.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
    }

    public static string ComputeStockStatus(int stockQuantity, int minStockLevel)
    {
        if (stockQuantity <= 0) return StatusOutOfStock;
        if (stockQuantity < minStockLevel) return StatusLowStock;
        return StatusInStock;
    }

    public static bool IsLowStock(int stockQuantity, int minStockLevel) =>
        stockQuantity > 0 && stockQuantity < minStockLevel;

    public static bool IsOutOfStock(int stockQuantity) => stockQuantity <= 0;

    public static async Task<int> GetTotalStockAsync(
        System.Data.IDbConnection connection, int tenantId, int partId, CancellationToken ct)
    {
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT ISNULL(SUM(StockQuantity), 0)
            FROM PartInventory
            WHERE PartId = @PartId AND TenantId = @TenantId
            """, new { PartId = partId, TenantId = tenantId }, cancellationToken: ct));
    }

    public static async Task InsertMovementAsync(
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

    public static async Task MaybeInsertLowStockAlertAsync(
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

public sealed record PartRow(
    int Id,
    string PartNumber,
    string PartName,
    string? Category,
    string? Brand,
    string? Supplier,
    decimal UnitCost,
    int MinStockLevel,
    int StockQuantity,
    string? VehicleCompatibilityJson,
    string? Location)
{
    public PartDto ToDto()
    {
        var status = PartStockHelper.ComputeStockStatus(StockQuantity, MinStockLevel);
        return new PartDto(
            Id,
            PartNumber,
            PartName,
            Category,
            Brand,
            Supplier,
            UnitCost,
            MinStockLevel,
            StockQuantity,
            PartStockHelper.IsLowStock(StockQuantity, MinStockLevel),
            PartStockHelper.ParseCompatibility(VehicleCompatibilityJson),
            status,
            PartStockHelper.IsOutOfStock(StockQuantity),
            Location);
    }
}
