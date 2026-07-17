namespace SheikhTravelSystem.Application.Common;

/// <summary>Self-describing report column — lets the frontend render/export any report generically, no per-report-type UI code.</summary>
public record ReportColumnDto(string Key, string Label, string Format); // "text"|"currency"|"date"|"number"

public record ReportRowDto(
    string Key,
    string Label,
    int Count,
    decimal TotalValue,
    IReadOnlyDictionary<string, object?> Fields);

public record ReportResponseDto(
    string ReportType,
    string Title,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<ReportRowDto> Rows,
    decimal TotalValue,
    IReadOnlyDictionary<string, object?> Summary);
