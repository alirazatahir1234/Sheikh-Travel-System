using System.Text.Json;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverApp.DTOs;

public record InspectionChecklistItemDto(string Key, string Label, bool Required);

public record InspectionTemplateDto(
    int Id,
    string Name,
    string? Description,
    IReadOnlyList<InspectionChecklistItemDto> Items);

public record InspectionResultItemDto(string Key, string Status, string? Comment);

public record DriverInspectionSummaryDto(
    int Id,
    int VehicleId,
    string? VehicleName,
    string? VehiclePlate,
    DateTime InspectionDate,
    string Result,
    decimal? OdometerReading,
    string? Comments,
    int PhotoCount,
    bool HasSignature);

public record DriverInspectionDetailDto(
    int Id,
    int VehicleId,
    string? VehicleName,
    DateTime InspectionDate,
    string Result,
    decimal? OdometerReading,
    string? Comments,
    IReadOnlyList<InspectionResultItemDto> Results,
    IReadOnlyList<string> PhotoUrls,
    string? SignatureUrl);

public record SubmitDriverInspectionRequest(
    int VehicleId,
    int? TemplateId,
    decimal? OdometerReading,
    string? Comments,
    IReadOnlyList<InspectionResultItemDto> Results,
    string? OverallResult = null);

public static class InspectionResultCalculator
{
    public static string ComputeOverall(IReadOnlyList<InspectionResultItemDto> results)
    {
        if (results.Count == 0) return "Pass";
        if (results.Any(r => string.Equals(r.Status, "Fail", StringComparison.OrdinalIgnoreCase)))
            return "Fail";
        if (results.Any(r => string.Equals(r.Status, "Warning", StringComparison.OrdinalIgnoreCase)))
            return "Warning";
        return "Pass";
    }

    public static string SerializeResults(IReadOnlyList<InspectionResultItemDto> results)
        => JsonSerializer.Serialize(results.Select(r => new
        {
            key = r.Key,
            status = r.Status,
            comment = r.Comment
        }));

    public static string SerializePhotos(IReadOnlyList<string> urls)
        => JsonSerializer.Serialize(urls);

    public static List<InspectionChecklistItemDto> ParseChecklist(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e => new InspectionChecklistItemDto(
                    e.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "",
                    e.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "",
                    e.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.True))
                .Where(i => !string.IsNullOrWhiteSpace(i.Key))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static List<InspectionResultItemDto> ParseResults(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e => new InspectionResultItemDto(
                    e.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "",
                    e.TryGetProperty("status", out var s) ? s.GetString() ?? "Pass" : "Pass",
                    e.TryGetProperty("comment", out var c) ? c.GetString() : null))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static List<string> ParsePhotos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
