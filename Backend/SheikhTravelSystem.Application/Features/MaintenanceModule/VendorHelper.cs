using System.Text.Json;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public static class VendorHelper
{
    public static string? SerializeProducts(IReadOnlyList<string>? products)
    {
        if (products is null || products.Count == 0) return null;
        var cleaned = products.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct().ToList();
        return cleaned.Count == 0 ? null : JsonSerializer.Serialize(cleaned);
    }

    public static IReadOnlyList<string> ParseProducts(string? productsJson)
    {
        if (string.IsNullOrWhiteSpace(productsJson)) return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(productsJson)?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return productsJson.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
    }
}

public sealed record VendorRow(
    int Id,
    string Name,
    string Category,
    string? ContactPerson,
    string? ContactPhone,
    string? ContactEmail,
    string? ProductsJson,
    decimal? Rating,
    bool IsPreferred,
    bool IsActive)
{
    public VendorDto ToDto() => new(
        Id, Name, Category, ContactPerson, ContactPhone, ContactEmail,
        VendorHelper.ParseProducts(ProductsJson), Rating, IsPreferred, IsActive);
}
