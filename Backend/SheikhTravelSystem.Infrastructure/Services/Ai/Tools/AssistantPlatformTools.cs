using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Tools;

/// <summary>
/// Normalizes spoken / typed company names for SuperAdmin tenant lookup
/// (e.g. "Shake Travel" / "शेक ट्रैवल" → Sheikh Travel).
/// </summary>
public static class AssistantTenantNameNormalizer
{
    /// <summary>Extract a company-name hint from a natural-language question, if present.</summary>
    public static string? ExtractTenantNameHint(string? question)
    {
        if (string.IsNullOrWhiteSpace(question)) return null;

        var m = Regex.Match(
            question,
            @"(shake|sheikh|शेक|शेख)\s*(travel|ट्रैवल|travels)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
            return m.Value.Trim();

        m = Regex.Match(
            question,
            @"(?:for|of|company|tenant)\s+([A-Za-z][A-Za-z0-9\s'-]{1,40})",
            RegexOptions.IgnoreCase);
        if (m.Success)
            return m.Groups[1].Value.Trim();

        return null;
    }

    /// <summary>Map common misspellings / Devanagari to the canonical DB-friendly form.</summary>
    public static string Normalize(string hint)
    {
        var s = hint.Trim();
        s = s.Replace("शेक", "Sheikh", StringComparison.Ordinal);
        s = s.Replace("शेख", "Sheikh", StringComparison.Ordinal);
        s = Regex.Replace(s, @"\bshake\b", "Sheikh", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\btravels?\b", "Travel", RegexOptions.IgnoreCase);
        s = s.Replace("ट्रैवल", "Travel", StringComparison.Ordinal);
        return Regex.Replace(s, @"\s+", " ").Trim();
    }
}

/// <summary>Resolves a target tenant for platform AI tools (JWT tenant, or SuperAdmin name/id override).</summary>
public static class AssistantTenantLookup
{
    public static async Task<(int TenantId, string? Error)> ResolveTargetTenantIdAsync(
        AiToolExecutionContext context,
        JsonElement? args,
        IDbConnectionFactory dbFactory,
        CancellationToken cancellationToken)
    {
        var targetTenantId = context.TenantId;
        string? nameHint = null;
        int? requestedTenantId = null;

        if (args is { ValueKind: JsonValueKind.Object } el)
        {
            if (el.TryGetProperty("tenantId", out var tidEl) && tidEl.TryGetInt32(out var tid) && tid > 0)
                requestedTenantId = tid;
            if (el.TryGetProperty("tenantNameHint", out var hintEl) && hintEl.ValueKind == JsonValueKind.String)
                nameHint = hintEl.GetString();
        }

        if (requestedTenantId is null && string.IsNullOrWhiteSpace(nameHint))
            return (targetTenantId, null);

        if (!context.IsPlatformSuperAdmin)
            return (context.TenantId, null);

        if (requestedTenantId is not null)
            return (requestedTenantId.Value, null);

        var resolved = await ResolveTenantIdByNameAsync(dbFactory, nameHint!, cancellationToken);
        if (resolved is null)
        {
            return (0,
                $"No active company matched “{nameHint}”. Try the exact company name (e.g. Sheikh Travel).");
        }

        return (resolved.Value, null);
    }

    public static async Task<int?> ResolveTenantIdByNameAsync(
        IDbConnectionFactory dbFactory, string hint, CancellationToken cancellationToken)
    {
        var original = hint.Trim();
        var normalized = AssistantTenantNameNormalizer.Normalize(original);
        if (string.IsNullOrWhiteSpace(normalized) && string.IsNullOrWhiteSpace(original))
            return null;

        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id
            FROM Tenants
            WHERE IsActive = 1
              AND (
                    Name = @Normalized
                 OR Name LIKE @Normalized + N'%'
                 OR Name LIKE N'%' + @Normalized + N'%'
                 OR Name LIKE N'%' + @Original + N'%'
                 OR REPLACE(LOWER(Name), N'sheikh', N'shake') LIKE N'%' + LOWER(@Original) + N'%'
              )
            ORDER BY
                CASE WHEN Name = @Normalized THEN 0
                     WHEN Name LIKE @Normalized + N'%' THEN 1
                     ELSE 2 END,
                Id
            """, new { Normalized = normalized, Original = original }, cancellationToken: cancellationToken));
    }
}

/// <summary>Parses 1-based image ordinals from EN/HI questions (e.g. 9th, ninth, नवीं).</summary>
public static class AssistantImageIndexParser
{
    private static readonly Dictionary<string, int> WordOrdinals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["first"] = 1, ["1st"] = 1, ["पहली"] = 1, ["पहला"] = 1, ["پہلی"] = 1,
        ["second"] = 2, ["2nd"] = 2, ["दूसरी"] = 2, ["दूसरा"] = 2, ["دوسری"] = 2,
        ["third"] = 3, ["3rd"] = 3, ["तीसरी"] = 3, ["तीसरा"] = 3, ["تیسری"] = 3,
        ["fourth"] = 4, ["4th"] = 4, ["चौथी"] = 4, ["चौथा"] = 4, ["چوتھی"] = 4,
        ["fifth"] = 5, ["5th"] = 5, ["पांचवी"] = 5, ["पाँचवी"] = 5, ["पांचवा"] = 5, ["پانچویں"] = 5,
        ["sixth"] = 6, ["6th"] = 6, ["छठी"] = 6, ["छठा"] = 6, ["چھٹی"] = 6,
        ["seventh"] = 7, ["7th"] = 7, ["सातवीं"] = 7, ["सातवां"] = 7, ["ساتویں"] = 7,
        ["eighth"] = 8, ["8th"] = 8, ["आठवीं"] = 8, ["आठवां"] = 8, ["آٹھویں"] = 8,
        ["ninth"] = 9, ["9th"] = 9, ["नवीं"] = 9, ["नौवीं"] = 9, ["नवां"] = 9, ["نواں"] = 9,
        ["tenth"] = 10, ["10th"] = 10, ["दसवीं"] = 10, ["दसवां"] = 10, ["دسویں"] = 10,
    };

    public static int ExtractIndex(string? question, int defaultIndex = 1)
    {
        if (string.IsNullOrWhiteSpace(question))
            return defaultIndex;

        // Prefer numeric ordinals with word boundaries so "15th" ≠ "5th".
        var ordinal = Regex.Match(
            question,
            @"\b(\d{1,3})(?:st|nd|rd|th)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (ordinal.Success && int.TryParse(ordinal.Groups[1].Value, out var n2) && n2 > 0)
            return n2;

        var numbered = Regex.Match(
            question,
            @"(?:image|photo|picture|media|तस्वीर|تصویر)\s*(?:number|#|no\.?|num\.?)?\s*(\d{1,3})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (numbered.Success && int.TryParse(numbered.Groups[1].Value, out var n1) && n1 > 0)
            return n1;

        var hash = Regex.Match(question, @"#\s*(\d{1,3})\b");
        if (hash.Success && int.TryParse(hash.Groups[1].Value, out var n3) && n3 > 0)
            return n3;

        foreach (var (word, index) in WordOrdinals.OrderByDescending(kv => kv.Key.Length))
        {
            if (word.Any(c => c > 127))
            {
                if (question.Contains(word, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            else if (Regex.IsMatch(
                         question,
                         $@"\b{Regex.Escape(word)}\b",
                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return index;
            }
        }

        return defaultIndex;
    }

    public static int ExtractIndexFromArgs(JsonElement? args, string? question, int defaultIndex = 1)
    {
        if (args is { ValueKind: JsonValueKind.Object } el
            && el.TryGetProperty("index", out var idxEl)
            && idxEl.TryGetInt32(out var fromArgs)
            && fromArgs > 0)
        {
            return fromArgs;
        }

        return ExtractIndex(question, defaultIndex);
    }
}

/// <summary>
/// Read-only: lists subscription / entitlement modules for a tenant (on/off + license).
/// Not MCP <c>list_modules</c> (codebase architecture — remains denied).
/// </summary>
public sealed class GetTenantSubscriptionModulesTool(
    IPlatformRepository platformRepository,
    IDbConnectionFactory dbFactory) : IAiTool
{
    public string Name => "GetTenantSubscriptionModules";

    public string Description =>
        "Lists the company's subscription modules with on/off (enabled) and licensed status for the current tenant. " +
        "Super-admins may target another company via tenantId or tenantNameHint (Shake→Sheikh fuzzy).";

    public string Kind => "read";
    public bool RequiresConfirmation => false;

    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        // EN
        "subscription module", "subscription modules", "subscribed modules",
        "enabled modules", "module entitlement", "which modules", "what modules",
        "modules are on", "modules enabled", "modules off", "tenant modules",
        "company modules", "licensed modules",
        // HI (Devanagari)
        "सब्सक्रिप्शन", "मॉड्यूल", "सब्सक्रिप्शन मॉड्यूल", "कौन से मॉड्यूल", "कौन सी सब्सक्रिप्शन",
        // UR (Nastaliq) + Roman Urdu
        "سبسکرپشن", "ماڈیول", "modules on hain", "subscription modules konsi"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (targetTenantId, resolveError) = await AssistantTenantLookup.ResolveTargetTenantIdAsync(
            context, args, dbFactory, cancellationToken);
        if (resolveError is not null)
        {
            return new AiToolExecutionResult(Name, false, string.Empty, Error: resolveError);
        }

        var tenant = await platformRepository.GetTenantNameAndPlanAsync(targetTenantId, cancellationToken);
        if (tenant is null)
        {
            return new AiToolExecutionResult(
                Name, false, string.Empty,
                Error: $"Company #{targetTenantId} was not found.");
        }

        var enabledCodes = (await platformRepository.GetInstalledModuleCodesAsync(targetTenantId, cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (enabledCodes.Count == 0)
        {
            var legacyJson = await platformRepository.GetEnabledModulesJsonAsync(targetTenantId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(legacyJson))
            {
                try
                {
                    var keys = JsonSerializer.Deserialize<string[]>(legacyJson) ?? [];
                    foreach (var code in TenantModuleCatalog.CodesFromLegacyKeys(keys))
                        enabledCodes.Add(code);
                }
                catch
                {
                    // ignore malformed legacy json
                }
            }
        }

        var catalog = await platformRepository.LoadModuleCatalogAsync(cancellationToken);
        var planName = await platformRepository.ResolvePlanNameAsync(targetTenantId, cancellationToken)
                       ?? tenant.Value.PlanName;
        var licensedCodes = SubscriptionPlanCatalog.LicensedModuleCodes(planName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var modules = catalog
            .Select(m => ModuleRegistryQueries.ToStatusDto(
                m, enabledCodes.Contains(m.Code), licensedCodes.Contains(m.Code)))
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Code)
            .ToList();

        if (modules.Count == 0)
        {
            modules = TenantModuleCatalog.All
                .Select(m =>
                {
                    var seed = ModuleRegistrySeed.All.FirstOrDefault(s => s.Code == m.Code);
                    var dto = seed is null
                        ? ModuleRegistryQueries.FromSeed(new ModuleRegistrySeed.Entry(
                            m.Code, m.Name, m.Name, m.Name, "Platform", "1.0.0", "extension", null, 0,
                            [], true, false, false, false, "Active", null, m.LegacyKeys))
                        : ModuleRegistryQueries.FromSeed(seed);
                    return ModuleRegistryQueries.ToStatusDto(
                        dto, enabledCodes.Contains(m.Code), licensedCodes.Contains(m.Code));
                })
                .ToList();
        }

        var enabled = modules.Where(m => m.IsEnabled).ToList();
        var disabled = modules.Where(m => !m.IsEnabled).ToList();

        var sb = new StringBuilder();
        sb.AppendLine(
            $"{tenant.Value.Name} (#{targetTenantId}) — plan {planName ?? "n/a"}: " +
            $"{enabled.Count}/{modules.Count} modules enabled.");
        if (enabled.Count > 0)
            sb.AppendLine("Enabled (on): " + string.Join(", ", enabled.Select(FormatModule)));
        if (disabled.Count > 0)
            sb.AppendLine("Off: " + string.Join(", ", disabled.Select(FormatModule)));

        var slim = modules.Select(m => new
        {
            code = m.Code,
            name = string.IsNullOrWhiteSpace(m.DisplayName) ? m.Name : m.DisplayName!,
            isEnabled = m.IsEnabled,
            isLicensed = m.IsLicensed
        }).ToList();

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new
            {
                tenantId = targetTenantId,
                tenantName = tenant.Value.Name,
                planName,
                enabledCount = enabled.Count,
                totalCount = modules.Count,
                modules = slim
            });
    }

    private static string FormatModule(ModuleStatusDto m)
    {
        var label = string.IsNullOrWhiteSpace(m.DisplayName) ? m.Name : m.DisplayName!;
        return $"{label} ({m.Code})";
    }
}

/// <summary>
/// Read-only: returns the Nth company image (1-based) from the tenant media gallery,
/// then vehicle photos — ordered newest-first within each source.
/// </summary>
public sealed class GetTenantCompanyImageTool(
    IPlatformRepository platformRepository,
    IDbConnectionFactory dbFactory,
    IFileStorageService fileStorage) : IAiTool
{
    public string Name => "GetTenantCompanyImage";

    public string Description =>
        "Shows a numbered company image (1st, 2nd, 9th, …) from the company's media library " +
        "and fleet photos. Super-admins may target another company via tenantNameHint (Shake→Sheikh).";

    public string Kind => "read";
    public bool RequiresConfirmation => false;

    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "तस्वीर", "تصویر", "company image", "company photo", "company picture",
        "show image", "show photo", "show picture", "nth image", "9th image",
        "gallery image", "media image", "picture", "photo", "image of",
        "images of", "media library"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        var (targetTenantId, resolveError) = await AssistantTenantLookup.ResolveTargetTenantIdAsync(
            context, args, dbFactory, cancellationToken);
        if (resolveError is not null)
            return new AiToolExecutionResult(Name, false, string.Empty, Error: resolveError);

        var question = args is { ValueKind: JsonValueKind.Object } a
            && a.TryGetProperty("question", out var qEl)
            && qEl.ValueKind == JsonValueKind.String
            ? qEl.GetString()
            : null;
        var index = AssistantImageIndexParser.ExtractIndexFromArgs(args, question, defaultIndex: 1);

        var tenant = await platformRepository.GetTenantNameAndPlanAsync(targetTenantId, cancellationToken);
        if (tenant is null)
        {
            return new AiToolExecutionResult(
                Name, false, string.Empty,
                Error: $"Company #{targetTenantId} was not found.");
        }

        var images = await LoadOrderedImagesAsync(targetTenantId, cancellationToken);
        if (images.Count == 0)
        {
            return new AiToolExecutionResult(
                Name, false, string.Empty,
                Error: $"{tenant.Value.Name} has no images in the media library or vehicle photos yet.");
        }

        if (index > images.Count)
        {
            return new AiToolExecutionResult(
                Name, false, string.Empty,
                Error: $"{tenant.Value.Name} has only {images.Count} image(s). Ask for a number from 1 to {images.Count}.");
        }

        var item = images[index - 1];
        var url = fileStorage.ResolveReadUrl(item.StorageKeyOrUrl);
        if (string.IsNullOrWhiteSpace(url))
            url = item.StorageKeyOrUrl;

        var alt = string.IsNullOrWhiteSpace(item.Label)
            ? $"{tenant.Value.Name} image {index}"
            : item.Label!;

        var sb = new StringBuilder();
        sb.AppendLine($"**{tenant.Value.Name} — image {index} of {images.Count}**");
        sb.AppendLine($"Source: {item.Source}");
        if (!string.IsNullOrWhiteSpace(item.Label))
            sb.AppendLine(item.Label);
        sb.AppendLine($"![{alt}]({url})");

        return new AiToolExecutionResult(
            Name, true, sb.ToString().Trim(),
            new
            {
                tenantId = targetTenantId,
                tenantName = tenant.Value.Name,
                index,
                totalCount = images.Count,
                source = item.Source,
                id = item.Id,
                fileName = item.FileName,
                url,
                alt,
                createdAt = item.CreatedAt
            });
    }

    private async Task<IReadOnlyList<CompanyImageRow>> LoadOrderedImagesAsync(
        int tenantId, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        // Media library first (company gallery), then fleet vehicle photos — newest within each source.
        var rows = (await connection.QueryAsync<CompanyImageRow>(new CommandDefinition("""
            SELECT Id, Source, StorageKeyOrUrl, FileName, Label, CreatedAt
            FROM (
                SELECT
                    m.Id,
                    N'WebsiteMedia' AS Source,
                    COALESCE(NULLIF(m.StorageKey, N''), m.FileUrl) AS StorageKeyOrUrl,
                    m.FileName,
                    COALESCE(NULLIF(m.AltText, N''), m.FileName) AS Label,
                    m.CreatedAt,
                    0 AS SourceOrder
                FROM WebsiteMedia m
                WHERE m.TenantId = @TenantId
                  AND (
                        m.FileType LIKE N'image/%'
                     OR m.FileName LIKE N'%.jpg'
                     OR m.FileName LIKE N'%.jpeg'
                     OR m.FileName LIKE N'%.png'
                     OR m.FileName LIKE N'%.webp'
                     OR m.FileName LIKE N'%.gif'
                     OR m.FileType IS NULL
                     OR LTRIM(RTRIM(m.FileType)) = N''
                  )

                UNION ALL

                SELECT
                    d.Id,
                    N'VehicleImage' AS Source,
                    d.FileUrl AS StorageKeyOrUrl,
                    COALESCE(v.RegistrationNumber, CONCAT(N'vehicle-', d.VehicleId)) AS FileName,
                    CONCAT(
                        COALESCE(v.RegistrationNumber, CONCAT(N'Vehicle #', d.VehicleId)),
                        N' photo') AS Label,
                    d.CreatedAt,
                    1 AS SourceOrder
                FROM VehicleDocuments d
                LEFT JOIN Vehicles v ON v.Id = d.VehicleId AND v.TenantId = d.TenantId AND v.IsDeleted = 0
                WHERE d.TenantId = @TenantId
                  AND d.IsDeleted = 0
                  AND d.DocumentType = N'VehicleImage'
                  AND d.FileUrl IS NOT NULL
                  AND LTRIM(RTRIM(d.FileUrl)) <> N''
            ) x
            ORDER BY SourceOrder, CreatedAt DESC, Id DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        return rows;
    }

    private sealed class CompanyImageRow
    {
        public int Id { get; init; }
        public string Source { get; init; } = "";
        public string StorageKeyOrUrl { get; init; } = "";
        public string? FileName { get; init; }
        public string? Label { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
