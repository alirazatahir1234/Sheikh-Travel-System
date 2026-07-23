using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Authentication;

/// <summary>
/// Stage 8 Permission Engine: UserRoles → RolePermissions → soft module/feature policy intersect.
/// </summary>
public class PermissionEngine(IDbConnectionFactory dbFactory) : IPermissionEngine
{
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
            ORDER BY r.Code
            """, new { UserId = userId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        var codes = roleCodes.Select(r => r.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

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

        var isSuperAdmin = codes.Contains(PlatformRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase);
        var isTenantAdmin = codes.Contains(PlatformRoles.TenantAdmin, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, HashSet<string>> sourceByPerm;
        List<string> permissionCodes;

        if (isSuperAdmin)
        {
            permissionCodes = (await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT PermissionCode FROM Permissions ORDER BY PermissionCode",
                cancellationToken: cancellationToken))).ToList();
            sourceByPerm = permissionCodes.ToDictionary(
                p => p,
                _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { PlatformRoles.SuperAdmin },
                StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            sourceByPerm = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (codes.Count > 0)
            {
                var pairs = await connection.QueryAsync<(string PermissionCode, string RoleCode)>(new CommandDefinition("""
                    SELECT DISTINCT p.PermissionCode, r.Code AS RoleCode
                    FROM Roles r
                    INNER JOIN RolePermissions rp ON rp.RoleId = r.Id
                    INNER JOIN Permissions p ON p.Id = rp.PermissionId
                    WHERE r.TenantId = @TenantId AND r.IsActive = 1 AND r.Code IN @RoleCodes
                    """, new { TenantId = tenantId, RoleCodes = codes }, cancellationToken: cancellationToken));

                foreach (var pair in pairs)
                    AddSource(sourceByPerm, pair.PermissionCode, pair.RoleCode);
            }

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

            // Mirror AuthorizationHandler Tenant Admin Platform.* bypass for effective lists.
            if (isTenantAdmin)
            {
                var platformPerms = await connection.QueryAsync<string>(new CommandDefinition("""
                    SELECT PermissionCode FROM Permissions
                    WHERE PermissionCode LIKE N'Platform.%'
                      AND PermissionCode NOT LIKE N'Platform.Tenants.%'
                    """, cancellationToken: cancellationToken));
                foreach (var p in platformPerms)
                    AddSource(sourceByPerm, p, PlatformRoles.TenantAdmin);
            }

            permissionCodes = sourceByPerm.Keys.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (!isSuperAdmin)
            permissionCodes = await ApplySoftPolicyGatesAsync(
                connection, tenantId, permissionCodes, cancellationToken);

        var effective = BuildEffectiveDtos(permissionCodes, sourceByPerm);
        return new PermissionEvaluationResult(userId, tenantId, codes, permissionCodes, effective);
    }

    private static async Task<List<string>> ApplySoftPolicyGatesAsync(
        IDbConnection connection,
        int tenantId,
        IReadOnlyList<string> permissionCodes,
        CancellationToken cancellationToken)
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

        // Load ModuleKey from DB when present; fall back to seed.
        Dictionary<string, string?> moduleKeys;
        try
        {
            moduleKeys = (await connection.QueryAsync<(string PermissionCode, string? ModuleKey)>(
                new CommandDefinition(
                    "SELECT PermissionCode, ModuleKey FROM Permissions WHERE PermissionCode IN @Codes",
                    new { Codes = permissionCodes },
                    cancellationToken: cancellationToken)))
                .ToDictionary(x => x.PermissionCode, x => x.ModuleKey, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            moduleKeys = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        var kept = new List<string>();
        foreach (var code in permissionCodes)
        {
            var seed = PermissionRegistrySeed.Find(code);
            moduleKeys.TryGetValue(code, out var dbModuleKey);
            var moduleKey = !string.IsNullOrWhiteSpace(dbModuleKey) ? dbModuleKey : seed?.ModuleKey;
            var featureKey = seed?.FeatureKey;

            // Unmapped → pass through.
            if (string.IsNullOrWhiteSpace(moduleKey) && string.IsNullOrWhiteSpace(featureKey))
            {
                kept.Add(code);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(moduleKey)
                && enabledModules.Count > 0
                && !enabledModules.Contains(moduleKey))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(featureKey)
                && enabledFeatures.Count > 0
                && !enabledFeatures.Contains(featureKey))
            {
                continue;
            }

            kept.Add(code);
        }

        return kept;
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
