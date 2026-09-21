using System.Text.Json;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Platform;

/// <summary>Shared mapping helpers for Stage 3 Module Registry (SQL lives in IPlatformRepository).</summary>
internal static class ModuleRegistryQueries
{
    public sealed class ModuleRow
    {
        public int Id { get; init; }
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string? Description { get; init; }
        public string? Category { get; init; }
        public string Version { get; init; } = "1.0.0";
        public string? Icon { get; init; }
        public string? Route { get; init; }
        public int SortOrder { get; init; }
        public string? DependenciesJson { get; init; }
        public bool Visible { get; init; } = true;
        public bool IsMobileSupported { get; init; }
        public bool IsAISupported { get; init; }
        public bool IsGPSSupported { get; init; }
        public string Status { get; init; } = "Active";
        public string? DocumentationUrl { get; init; }
        public string? LegacyKeysJson { get; init; }
    }

    public static IReadOnlyList<string> ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static ModuleRegistryDto ToRegistryDto(ModuleRow row, bool installed = false, bool licensed = false)
    {
        var enableable = ModuleRegistrySeed.IsEnableable(row.Code)
            && string.Equals(row.Status, "Active", StringComparison.OrdinalIgnoreCase);
        return new ModuleRegistryDto(
            Code: row.Code,
            Name: row.Name,
            DisplayName: string.IsNullOrWhiteSpace(row.DisplayName) ? row.Name : row.DisplayName,
            Description: row.Description,
            Category: row.Category,
            Version: row.Version,
            Icon: row.Icon,
            Route: row.Route,
            SortOrder: row.SortOrder,
            Dependencies: ParseJsonArray(row.DependenciesJson),
            Visible: row.Visible,
            IsMobileSupported: row.IsMobileSupported,
            IsAISupported: row.IsAISupported,
            IsGPSSupported: row.IsGPSSupported,
            Status: row.Status,
            DocumentationUrl: row.DocumentationUrl,
            LegacyKeys: ParseJsonArray(row.LegacyKeysJson),
            IsEnableable: enableable,
            Id: row.Id,
            IsInstalled: installed,
            IsLicensed: licensed);
    }

    public static ModuleRegistryDto FromSeed(ModuleRegistrySeed.Entry e, bool installed = false, bool licensed = false)
    {
        var enableable = ModuleRegistrySeed.IsEnableable(e.Code)
            && string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase);
        return new ModuleRegistryDto(
            Code: e.Code,
            Name: e.Name,
            DisplayName: e.DisplayName,
            Description: e.Description,
            Category: e.Category,
            Version: e.Version,
            Icon: e.Icon,
            Route: e.Route,
            SortOrder: e.SortOrder,
            Dependencies: e.Dependencies,
            Visible: e.Visible,
            IsMobileSupported: e.IsMobileSupported,
            IsAISupported: e.IsAISupported,
            IsGPSSupported: e.IsGPSSupported,
            Status: e.Status,
            DocumentationUrl: e.DocumentationUrl,
            LegacyKeys: e.LegacyKeys,
            IsEnableable: enableable,
            Id: null,
            IsInstalled: installed,
            IsLicensed: licensed);
    }

    public static TenantModuleDefinitionDto ToDefinitionDto(ModuleRegistryDto m)
        => new(
            m.Code,
            m.Name,
            m.LegacyKeys,
            m.DisplayName,
            m.Description,
            m.Category,
            m.Version,
            m.Icon,
            m.Route,
            m.SortOrder,
            m.Dependencies,
            m.Visible,
            m.IsMobileSupported,
            m.IsAISupported,
            m.IsGPSSupported,
            m.Status,
            m.DocumentationUrl,
            m.IsEnableable,
            m.Id);

    public static ModuleStatusDto ToStatusDto(ModuleRegistryDto m, bool enabled, bool licensed)
        => new(
            m.Code,
            m.Name,
            enabled,
            m.DisplayName,
            m.Description,
            m.Category,
            m.Version,
            m.Icon,
            m.Route,
            m.SortOrder,
            m.Dependencies,
            m.Visible,
            m.IsMobileSupported,
            m.IsAISupported,
            m.IsGPSSupported,
            m.Status,
            m.DocumentationUrl,
            IsInstalled: enabled,
            IsLicensed: licensed,
            CanToggle: m.IsEnableable);
}
