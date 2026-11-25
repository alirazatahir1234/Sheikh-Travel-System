using Dapper;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>SQL fragments shared by TravelNexus repositories.</summary>
internal static class TravelSql
{
    /// <summary>
    /// Branch clamp: company-wide users see everything; branch-scoped users see their branches plus records
    /// with no branch. <paramref name="column"/> is a trusted column reference such as <c>p.BranchId</c>.
    /// </summary>
    public static void ApplyBranchScope(TravelBranchScope scope, string column, List<string> clauses, DynamicParameters p)
    {
        if (scope.IsCompanyWide) return;
        if (scope.BranchIds.Count == 0)
        {
            clauses.Add($"{column} IS NULL");
            return;
        }
        clauses.Add($"({column} IS NULL OR {column} IN @ScopeBranchIds)");
        p.Add("ScopeBranchIds", scope.BranchIds.ToArray());
    }

    /// <summary>
    /// Inclusive date range on a UTC datetime column (audit SG-15: always applied when supplied).
    /// <paramref name="to"/> includes the whole day.
    /// </summary>
    public static void ApplyDateRange(DateTime? from, DateTime? to, string column, List<string> clauses, DynamicParameters p)
    {
        if (from.HasValue)
        {
            clauses.Add($"{column} >= @RangeFrom");
            p.Add("RangeFrom", from.Value.Date);
        }
        if (to.HasValue)
        {
            clauses.Add($"{column} < @RangeToExclusive");
            p.Add("RangeToExclusive", to.Value.Date.AddDays(1));
        }
    }

    public static string EscapeLike(string s)
        => s.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
