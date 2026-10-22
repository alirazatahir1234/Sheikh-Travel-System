namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Picks the highest-privilege role code when a user has multiple assignments.
/// Uses <see cref="RoleRegistrySeed.SortOrder"/> (lower = more privileged), not numeric Role.Id.
/// </summary>
public static class RoleCodePriority
{
    /// <summary>Unknown / custom roles sort after seeded system roles.</summary>
    public const int UnknownSortOrder = 10_000;

    public static int SortOrderOf(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
            return UnknownSortOrder;

        var seeded = RoleRegistrySeed.Find(roleCode.Trim());
        if (seeded is not null)
            return seeded.SortOrder;

        // Common aliases not in the seed table.
        return roleCode.Trim().ToUpperInvariant() switch
        {
            "ADMIN" or "SUPERADMIN" => RoleRegistrySeed.Find("TENANT_ADMIN")?.SortOrder ?? 10,
            "GPS_OPERATOR" => 22,
            "FIELD_DRIVER" => RoleRegistrySeed.Find("DRIVER")?.SortOrder ?? 50,
            _ => UnknownSortOrder
        };
    }

    /// <summary>
    /// Returns the most privileged role code, or <paramref name="fallback"/> when empty.
    /// </summary>
    public static string? SelectPrimary(IEnumerable<string>? roleCodes, string? fallback = null)
    {
        var best = roleCodes?
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(SortOrderOf)
            .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return best ?? fallback;
    }

    /// <summary>Orders role codes highest privilege first.</summary>
    public static IReadOnlyList<string> OrderByPrivilege(IEnumerable<string>? roleCodes)
    {
        return (roleCodes ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(SortOrderOf)
            .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
