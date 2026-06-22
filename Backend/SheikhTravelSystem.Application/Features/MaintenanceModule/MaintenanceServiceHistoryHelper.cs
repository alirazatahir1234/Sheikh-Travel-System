using System.Text.Json;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public static class MaintenanceServiceHistoryHelper
{
    public static string? ExtractFirstDocumentUrl(string? documentsJson)
    {
        if (string.IsNullOrWhiteSpace(documentsJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(documentsJson);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => FirstFromArray(doc.RootElement),
                JsonValueKind.Object when doc.RootElement.TryGetProperty("url", out var urlProp) => urlProp.GetString(),
                JsonValueKind.String => doc.RootElement.GetString(),
                _ => null
            };
        }
        catch (JsonException)
        {
            return documentsJson.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? documentsJson : null;
        }
    }

    private static string? FirstFromArray(JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                    break;
                case JsonValueKind.Object:
                    if (item.TryGetProperty("url", out var url)) return url.GetString();
                    if (item.TryGetProperty("Url", out var url2)) return url2.GetString();
                    break;
            }
        }

        return null;
    }

    public static VehicleServiceHistoryItemDto MapRow(ServiceHistoryRow row) =>
        new(
            row.Id,
            row.Source,
            row.VehicleId,
            row.VehicleName,
            row.VehicleRegistration,
            row.ServiceType ?? "Service",
            row.ServiceDate,
            row.WorkshopName,
            row.TechnicianName,
            row.TotalCost,
            row.LaborCost,
            row.PartsCost,
            ExtractFirstDocumentUrl(row.DocumentsJson),
            row.Notes,
            row.Status);
}

public sealed record ServiceHistoryRow(
    int Id,
    string Source,
    int VehicleId,
    string? VehicleName,
    string? VehicleRegistration,
    string? ServiceType,
    DateTime ServiceDate,
    string? WorkshopName,
    string? TechnicianName,
    decimal TotalCost,
    decimal LaborCost,
    decimal PartsCost,
    string? DocumentsJson,
    string? Notes,
    string Status);
