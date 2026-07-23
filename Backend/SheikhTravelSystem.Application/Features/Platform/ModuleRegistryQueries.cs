using System.Data;
using System.Text.Json;
using Dapper;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Platform;

/// <summary>Shared SQL/mapping for Stage 3 Module Registry reads.</summary>
internal static class ModuleRegistryQueries
{
    public const string SelectSql = """
        SELECT Id, ModuleCode AS Code, ModuleName AS Name,
               COALESCE(DisplayName, ModuleName) AS DisplayName,
               Description, Category,
               COALESCE(Version, N'1.0.0') AS Version,
               Icon, Route, COALESCE(SortOrder, 0) AS SortOrder,
               DependenciesJson, COALESCE(Visible, 1) AS Visible,
               COALESCE(IsMobileSupported, 0) AS IsMobileSupported,
               COALESCE(IsAISupported, 0) AS IsAISupported,
               COALESCE(IsGPSSupported, 0) AS IsGPSSupported,
               COALESCE(Status, N'Active') AS Status,
               DocumentationUrl, LegacyKeysJson
        FROM Modules
        """;

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

    public static ModuleRegistryDto ToRegistryDto(ModuleRow row, bool installed = false)
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
            IsLicensed: installed);
    }

    public static ModuleRegistryDto FromSeed(ModuleRegistrySeed.Entry e, bool installed = false)
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
            IsLicensed: installed);
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

    public static ModuleStatusDto ToStatusDto(ModuleRegistryDto m, bool enabled)
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
            IsLicensed: enabled,
            CanToggle: m.IsEnableable);

    public static async Task<IReadOnlyList<ModuleRegistryDto>> LoadCatalogAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = (await connection.QueryAsync<ModuleRow>(new CommandDefinition(
                SelectSql + " WHERE COALESCE(Visible, 1) = 1 ORDER BY COALESCE(SortOrder, 0), ModuleCode",
                cancellationToken: cancellationToken))).ToList();

            if (rows.Count > 0)
                return rows.Select(r => ToRegistryDto(r)).ToList();
        }
        catch
        {
            // Metadata columns / table may not exist yet.
        }

        return ModuleRegistrySeed.All
            .Where(e => e.Visible)
            .OrderBy(e => e.SortOrder)
            .Select(e => FromSeed(e))
            .ToList();
    }

    public static async Task<ModuleRegistryDto?> LoadByKeyAsync(
        IDbConnection connection,
        string codeOrId,
        CancellationToken cancellationToken)
    {
        try
        {
            ModuleRow? row = null;
            if (int.TryParse(codeOrId, out var id))
            {
                row = await connection.QuerySingleOrDefaultAsync<ModuleRow>(new CommandDefinition(
                    SelectSql + " WHERE Id = @Id",
                    new { Id = id },
                    cancellationToken: cancellationToken));
            }

            row ??= await connection.QuerySingleOrDefaultAsync<ModuleRow>(new CommandDefinition(
                SelectSql + " WHERE ModuleCode = @Code",
                new { Code = codeOrId },
                cancellationToken: cancellationToken));

            if (row is not null)
                return ToRegistryDto(row);
        }
        catch
        {
            // fall through to seed
        }

        var seed = ModuleRegistrySeed.All.FirstOrDefault(e =>
            string.Equals(e.Code, codeOrId, StringComparison.OrdinalIgnoreCase));
        return seed is null ? null : FromSeed(seed);
    }
}
