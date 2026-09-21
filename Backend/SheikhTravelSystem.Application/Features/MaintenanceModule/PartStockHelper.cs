using System.Text.Json;

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
