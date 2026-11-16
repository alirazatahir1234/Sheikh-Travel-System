using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Authentication;

/// <summary>
/// Stage 8 Permission Engine: UserRoles → RolePermissions → soft module/feature policy intersect.
/// </summary>
public class PermissionEngine(
    IDbConnectionFactory dbFactory,
    ILogger<PermissionEngine>? logger = null) : IPermissionEngine
{
    private readonly ILogger<PermissionEngine> _logger = logger ?? NullLogger<PermissionEngine>.Instance;

    public sealed record StrippedPermissionDiagnostic(
        string PermissionCode,
        string Reason,
        string? ModuleKey,
        string? FeatureKey);

    public async Task<PermissionEvaluationResult> EvaluateAsync(
        int userId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var roleCodes = (await connection.QueryAsync<(string Code, int RoleId)>(new CommandDefinition("""
            SELECT r.Code, r.Id AS RoleId
            FROM UserRoles ur
            INNER JOIN Roles r ON r.Id = ur.RoleId AND r.IsActive = 1
            WHERE ur.UserId = @UserId AND r.TenantId = @TenantId
            ORDER BY COALESCE(r.SortOrder, 9999), r.Code
            """, new { UserId = userId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        // Privilege order (SortOrder), not Role.Id — primary JWT/login role is First().
        var codes = RoleCodePriority.OrderByPrivilege(roleCodes.Select(r => r.Code)).ToList();

        var legacyRole = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Role FROM Users WHERE Id = @UserId AND IsDeleted = 0",
            new { UserId = userId },
            cancellationToken: cancellationToken));

        if (codes.Count == 0 && legacyRole.HasValue)
        {
            codes.Add(MapLegacyRole((UserRole)legacyRole.Value));
        }
        else if (legacyRole.HasValue)
        {
            EnsureLegacyBridge(codes, (UserRole)legacyRole.Value);
        }

        // SG-05 A6: merge RolePermissions from working-tenant roles + platform-tenant roles.
        // No catalog dump for SUPER_ADMIN and no TENANT_ADMIN Platform.* mirror.
        var platformRoleCodes = (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT r.Code
            FROM UserRoles ur
            INNER JOIN Roles r ON r.Id = ur.RoleId AND r.IsActive = 1
            WHERE ur.UserId = @UserId AND r.TenantId = @PlatformTenantId
            """,
            new { UserId = userId, PlatformTenantId = PlatformTenant.Id },
            cancellationToken: cancellationToken))).ToList();

        foreach (var platformCode in platformRoleCodes)
        {
            if (!codes.Contains(platformCode, StringComparer.OrdinalIgnoreCase))
                codes.Add(platformCode);
        }

        codes = RoleCodePriority.OrderByPrivilege(codes).ToList();

        var sourceByPerm = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var tenantIdsForPerms = new HashSet<int> { tenantId, PlatformTenant.Id };

        var pairs = await connection.QueryAsync<(string PermissionCode, string RoleCode)>(new CommandDefinition("""
            SELECT DISTINCT p.PermissionCode, r.Code AS RoleCode
            FROM UserRoles ur
            INNER JOIN Roles r ON r.Id = ur.RoleId AND r.IsActive = 1
            INNER JOIN RolePermissions rp ON rp.RoleId = r.Id
            INNER JOIN Permissions p ON p.Id = rp.PermissionId
            WHERE ur.UserId = @UserId AND r.TenantId IN @TenantIds
            """,
            new { UserId = userId, TenantIds = tenantIdsForPerms.ToArray() },
            cancellationToken: cancellationToken));

        foreach (var pair in pairs)
            AddSource(sourceByPerm, pair.PermissionCode, pair.RoleCode);

        if (sourceByPerm.Count == 0 && codes.Count > 0)
        {
            foreach (var code in codes)
            {
                var match = TenantRolePermissionTemplates.StandardRoles
                    .FirstOrDefault(r => string.Equals(r.RoleCode, code, StringComparison.OrdinalIgnoreCase));
                if (match.Permissions is { Length: > 0 } perms)
                {
                    foreach (var p in perms)
                        AddSource(sourceByPerm, p, code);
                }
            }
        }

        var permissionCodes = sourceByPerm.Keys
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Platform operators keep full RolePermissions; tenant entitlements still apply
        // for non-platform working tenants (module/feature soft gates).
        var isPlatformOperator = sourceByPerm.ContainsKey(PlatformPermissions.TenantsManage)
            || sourceByPerm.ContainsKey(PlatformPermissions.TenantsDataRead);
        if (!isPlatformOperator || tenantId != PlatformTenant.Id)
            permissionCodes = await FilterByTenantEntitlementsAsync(
                connection, tenantId, permissionCodes, userId, cancellationToken);

        var effective = BuildEffectiveDtos(permissionCodes, sourceByPerm);
        return new PermissionEvaluationResult(userId, tenantId, codes, permissionCodes, effective);
    }

    public async Task<List<string>> FilterByTenantEntitlementsAsync(
        IDbConnection connection,
        int tenantId,
        IReadOnlyList<string> permissionCodes,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (permissionCodes.Count == 0) return [];

        HashSet<string> enabledModules;
        try
        {
            enabledModules = (await connection.QueryAsync<string>(new CommandDefinition("""
                SELECT m.ModuleCode
                FROM TenantModules tm
                INNER JOIN Modules m ON m.Id = tm.ModuleId
                WHERE tm.TenantId = @TenantId
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            enabledModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        HashSet<string> enabledFeatures;
        try
        {
            enabledFeatures = (await connection.QueryAsync<string>(new CommandDefinition("""
                SELECT FeatureKey
                FROM TenantFeatures
                WHERE TenantId = @TenantId AND COALESCE(IsEnabled, 1) = 1
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            enabledFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        // Load ModuleKey / FeatureKey from DB when present; fall back to seed.
        Dictionary<string, (string? ModuleKey, string? FeatureKey)> permissionGates;
        try
        {
            permissionGates = (await connection.QueryAsync<(string PermissionCode, string? ModuleKey, string? FeatureKey)>(
                new CommandDefinition(
                    "SELECT PermissionCode, ModuleKey, FeatureKey FROM Permissions WHERE PermissionCode IN @Codes",
                    new { Codes = permissionCodes },
                    cancellationToken: cancellationToken)))
                .ToDictionary(
                    x => x.PermissionCode,
                    x => (x.ModuleKey, x.FeatureKey),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // Older DBs without FeatureKey — ModuleKey-only query.
            try
            {
                permissionGates = (await connection.QueryAsync<(string PermissionCode, string? ModuleKey)>(
                    new CommandDefinition(
                        "SELECT PermissionCode, ModuleKey FROM Permissions WHERE PermissionCode IN @Codes",
                        new { Codes = permissionCodes },
                        cancellationToken: cancellationToken)))
                    .ToDictionary(
                        x => x.PermissionCode,
                        x => (x.ModuleKey, (string?)null),
                        StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                permissionGates = new Dictionary<string, (string? ModuleKey, string? FeatureKey)>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        return FilterByTenantEntitlements(
            tenantId,
            permissionCodes,
            enabledModules,
            enabledFeatures,
            permissionGates,
            userId);
    }

    public List<string> FilterByTenantEntitlements(
        int tenantId,
        IReadOnlyList<string> permissionCodes,
        IReadOnlySet<string> enabledModules,
        IReadOnlySet<string> enabledFeatures,
        IReadOnlyDictionary<string, (string? ModuleKey, string? FeatureKey)>? permissionGates = null,
        int? userId = null)
    {
        if (permissionCodes.Count == 0) return [];

        // Effective enabled modules include direct TenantModules rows plus any catalog module
        // implied by an enabled TenantFeatures legacy key (e.g. "bookings" -> BOOKINGS, "rental" -> RENTAL).
        var effectiveModules = new HashSet<string>(enabledModules, StringComparer.OrdinalIgnoreCase);
        if (effectiveModules.Count > 0 && enabledFeatures.Count > 0)
        {
            foreach (var derivedModule in TenantModuleCatalog.CodesFromLegacyKeys(enabledFeatures))
                effectiveModules.Add(derivedModule);
        }

        var kept = new List<string>(permissionCodes.Count);
        var stripped = new List<StrippedPermissionDiagnostic>();

        foreach (var code in permissionCodes)
        {
            var seed = PermissionRegistrySeed.Find(code);
            (string? ModuleKey, string? FeatureKey) dbGate = default;
            permissionGates?.TryGetValue(code, out dbGate);

            var moduleKey = ResolveEffectiveModuleKey(code, seed, dbGate.ModuleKey);
            var featureKey = !string.IsNullOrWhiteSpace(dbGate.FeatureKey) ? dbGate.FeatureKey : seed?.FeatureKey;

            // Unmapped → pass through.
            if (string.IsNullOrWhiteSpace(moduleKey) && string.IsNullOrWhiteSpace(featureKey))
            {
                kept.Add(code);
                continue;
            }

            // Only enforce TenantModules for codes in the enableable catalog.
            // Beta/orphan ModuleKeys (e.g. NOTIFICATIONS) must not wipe JWT claims.
            if (!string.IsNullOrWhiteSpace(moduleKey)
                && effectiveModules.Count > 0
                && !IsModuleEntitled(code, moduleKey, effectiveModules)
                && IsEnableableModuleCode(moduleKey))
            {
                stripped.Add(new StrippedPermissionDiagnostic(
                    code,
                    "StrippedByDisabledModule",
                    moduleKey,
                    null));
                continue;
            }

            // TenantFeatures are opt-in flags. Module-governed permissions or legacy DB rows
            // whose FeatureKey is a TenantModuleCatalog legacy key ("vehicles", "gps-tracking",
            // "bookings", "trips", etc.) are already governed by TenantModules above — never strip
            // them just because an unrelated TenantFeatures row exists.
            if (!string.IsNullOrWhiteSpace(featureKey)
                && enabledFeatures.Count > 0
                && !enabledFeatures.Contains(featureKey)
                && !IsModuleLegacyFeatureKey(featureKey, effectiveModules))
            {
                stripped.Add(new StrippedPermissionDiagnostic(
                    code,
                    "StrippedByDisabledFeature",
                    moduleKey,
                    featureKey));
                continue;
            }

            kept.Add(code);
        }

        if (stripped.Count > 0)
        {
            var byModule = stripped
                .Where(s => s.Reason == "StrippedByDisabledModule")
                .GroupBy(s => s.ModuleKey ?? "UNKNOWN", StringComparer.OrdinalIgnoreCase)
                .Select(g => $"StrippedByDisabledModule[ModuleKey={g.Key}]: {string.Join(", ", g.Select(x => x.PermissionCode))}");

            var byFeature = stripped
                .Where(s => s.Reason == "StrippedByDisabledFeature")
                .GroupBy(s => s.FeatureKey ?? "UNKNOWN", StringComparer.OrdinalIgnoreCase)
                .Select(g => $"StrippedByDisabledFeature[FeatureKey={g.Key}]: {string.Join(", ", g.Select(x => x.PermissionCode))}");

            var reasonSummary = string.Join(" | ", byModule.Concat(byFeature));
            var strippedCodes = stripped.Select(s => s.PermissionCode).ToArray();

            _logger.LogWarning(
                "PermissionEngine stripped {StrippedCount} permission claim(s) for TenantId={TenantId}, UserId={UserId}. Reasons: {ReasonSummary}. StrippedCodes: {StrippedPermissions}",
                stripped.Count,
                tenantId,
                userId,
                reasonSummary,
                string.Join(", ", strippedCodes));
        }

        return kept;
    }

    private static string? ResolveEffectiveModuleKey(
        string permissionCode,
        PermissionRegistrySeed.Entry? seed,
        string? dbModuleKey)
    {
        // When PermissionRegistrySeed explicitly defines the permission as ungated (e.g. Notification.*,
        // Platform.Dashboard.*, Platform.Branches.*), ignore stale DB ModuleKey values.
        if (seed is not null && string.IsNullOrWhiteSpace(seed.ModuleKey))
            return null;

        // Trip.* is governed by TRAVEL/FLEET/GPS in seed even if an older DB row still has BOOKINGS.
        if (permissionCode.StartsWith("Trip.", StringComparison.OrdinalIgnoreCase) && seed?.ModuleKey is not null)
            return seed.ModuleKey;

        if (!string.IsNullOrWhiteSpace(dbModuleKey))
            return NormalizeCatalogModuleCode(dbModuleKey);

        return seed?.ModuleKey is not null ? NormalizeCatalogModuleCode(seed.ModuleKey) : null;
    }

    private static string NormalizeCatalogModuleCode(string moduleKey) =>
        moduleKey.Trim().ToUpperInvariant() switch
        {
            "FLEET_CORE" => "FLEET",
            "GPS_TELEMATICS" => "GPS",
            "TRIPS_DISPATCH" => "TRAVEL",
            "BOOKINGS_RESERVATIONS" => "BOOKINGS",
            "FINANCE_BILLING" => "FINANCE",
            "REPORTS_ANALYTICS" => "ANALYTICS",
            _ => moduleKey.Trim()
        };

    private static bool IsModuleEntitled(
        string permissionCode,
        string moduleKey,
        IReadOnlySet<string> effectiveModules)
    {
        if (effectiveModules.Contains(moduleKey))
            return true;

        // Trip.* permissions are shared across Fleet, GPS, Travel/Routes, and Bookings workflows.
        if (permissionCode.StartsWith("Trip.", StringComparison.OrdinalIgnoreCase))
        {
            return effectiveModules.Contains("TRAVEL")
                || effectiveModules.Contains("BOOKINGS")
                || effectiveModules.Contains("FLEET")
                || effectiveModules.Contains("GPS");
        }

        return false;
    }

    private static bool IsEnableableModuleCode(string moduleKey) =>
        TenantModuleCatalog.All.Any(m =>
            string.Equals(m.Code, moduleKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True when <paramref name="featureKey"/> is a <see cref="TenantModuleCatalog"/>
    /// legacy key (or module code). Module-level keys are governed by <c>TenantModules</c>,
    /// not opt-in <c>TenantFeatures</c>.
    /// </summary>
    private static bool IsModuleLegacyFeatureKey(
        string featureKey,
        IReadOnlySet<string> enabledModules)
    {
        foreach (var module in TenantModuleCatalog.All)
        {
            var isLegacy = module.LegacyKeys.Any(k =>
                string.Equals(k, featureKey, StringComparison.OrdinalIgnoreCase));
            var isCode = string.Equals(module.Code, featureKey, StringComparison.OrdinalIgnoreCase);
            if (!isLegacy && !isCode)
                continue;

            // Legacy key belonging to a catalog module: entitled when that module is on
            // (or when no module filter is configured).
            if (enabledModules.Count == 0)
                return true;
            if (enabledModules.Contains(module.Code))
                return true;
        }

        return false;
    }

    private static IReadOnlyList<EffectivePermissionDto> BuildEffectiveDtos(
        IReadOnlyList<string> permissionCodes,
        Dictionary<string, HashSet<string>> sourceByPerm)
    {
        return permissionCodes.Select(code =>
        {
            var seed = PermissionRegistrySeed.Find(code);
            sourceByPerm.TryGetValue(code, out var sources);
            return new EffectivePermissionDto(
                code,
                seed?.DisplayName ?? PermissionRegistrySeed.DeriveDisplayName(code),
                seed?.Category,
                seed?.ModuleKey,
                seed?.Action ?? PermissionRegistrySeed.DeriveAction(code),
                sources?.OrderBy(s => s).ToList());
        }).ToList();
    }

    private static void AddSource(
        Dictionary<string, HashSet<string>> map,
        string permissionCode,
        string roleCode)
    {
        if (!map.TryGetValue(permissionCode, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            map[permissionCode] = set;
        }
        set.Add(roleCode);
    }

    private static void EnsureLegacyBridge(List<string> roleCodes, UserRole legacyRole)
    {
        var mapped = MapLegacyRole(legacyRole);
        if (!roleCodes.Contains(mapped, StringComparer.OrdinalIgnoreCase))
            roleCodes.Add(mapped);
    }

    private static string MapLegacyRole(UserRole role) => RoleRegistrySeed.MapLegacyRoleCode(role);
}
