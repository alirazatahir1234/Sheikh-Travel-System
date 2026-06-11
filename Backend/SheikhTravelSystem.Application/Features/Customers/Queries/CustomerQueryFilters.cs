using Dapper;

namespace SheikhTravelSystem.Application.Features.Customers.Queries;

internal static class CustomerQueryFilters
{
    public const int NewCustomerDays = 30;

    public static (string WhereClause, DynamicParameters Parameters) Build(
        string? search,
        bool? isActive,
        string? recency,
        bool applyRecency = true)
    {
        var where = "WHERE IsDeleted = 0";
        var parameters = new DynamicParameters();
        var newSince = DateTime.UtcNow.Date.AddDays(-NewCustomerDays);
        parameters.Add("NewSince", newSince);

        if (isActive.HasValue)
        {
            where += " AND IsActive = @IsActive";
            parameters.Add("IsActive", isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            where += @" AND (
                FullName LIKE @SearchPattern OR
                Phone LIKE @SearchPattern OR
                Email LIKE @SearchPattern OR
                CNIC LIKE @SearchPattern OR
                Address LIKE @SearchPattern)";
            parameters.Add("SearchPattern", $"%{search.Trim()}%");
        }

        if (applyRecency && !string.IsNullOrWhiteSpace(recency))
        {
            switch (recency.Trim().ToUpperInvariant())
            {
                case "NEW":
                    where += " AND CreatedAt >= @NewSince";
                    break;
                case "RETURNING":
                    where += " AND CreatedAt < @NewSince";
                    break;
            }
        }

        return (where, parameters);
    }
}
