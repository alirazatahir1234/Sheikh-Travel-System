namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Helpers for appending tenant isolation to Dapper SQL fragments.
/// </summary>
public static class TenantSql
{
    public const string TenantFilter = " AND TenantId = @TenantId";

    public static string AndTenant(string sqlFragment) =>
        sqlFragment.Contains("TenantId", StringComparison.OrdinalIgnoreCase)
            ? sqlFragment
            : sqlFragment + TenantFilter;
}
