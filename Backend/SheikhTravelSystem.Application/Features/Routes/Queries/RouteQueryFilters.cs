using Dapper;

namespace SheikhTravelSystem.Application.Features.Routes.Queries;

internal static class RouteQueryFilters
{
    public const decimal ShortKmMax = 150m;
    public const decimal MediumKmMax = 500m;
    public const decimal BudgetPrice = 5000m;
    public const decimal MidPrice = 15000m;

    public static (string WhereClause, DynamicParameters Parameters) Build(
        string? search,
        bool? isActive,
        string? distanceBand,
        string? priceBand,
        bool applyDistanceBand = true)
    {
        var where = "WHERE IsDeleted = 0";
        var parameters = new DynamicParameters();

        if (isActive.HasValue)
        {
            where += " AND IsActive = @IsActive";
            parameters.Add("IsActive", isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            where += @" AND (
                Name LIKE @SearchPattern OR
                Source LIKE @SearchPattern OR
                Destination LIKE @SearchPattern OR
                CAST(Distance AS NVARCHAR(32)) LIKE @SearchPattern OR
                CAST(BasePrice AS NVARCHAR(32)) LIKE @SearchPattern)";
            parameters.Add("SearchPattern", $"%{search.Trim()}%");
        }

        if (applyDistanceBand && !string.IsNullOrWhiteSpace(distanceBand))
        {
            switch (distanceBand.Trim().ToUpperInvariant())
            {
                case "SHORT":
                    where += " AND Distance > 0 AND Distance < @ShortKmMax";
                    parameters.Add("ShortKmMax", ShortKmMax);
                    break;
                case "MEDIUM":
                    where += " AND Distance >= @ShortKmMax AND Distance <= @MediumKmMax";
                    parameters.Add("ShortKmMax", ShortKmMax);
                    parameters.Add("MediumKmMax", MediumKmMax);
                    break;
                case "LONG":
                    where += " AND Distance > @MediumKmMax";
                    parameters.Add("MediumKmMax", MediumKmMax);
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(priceBand))
        {
            switch (priceBand.Trim().ToUpperInvariant())
            {
                case "BUDGET":
                    where += " AND BasePrice <= @BudgetPrice";
                    parameters.Add("BudgetPrice", BudgetPrice);
                    break;
                case "MID":
                    where += " AND BasePrice > @BudgetPrice AND BasePrice <= @MidPrice";
                    parameters.Add("BudgetPrice", BudgetPrice);
                    parameters.Add("MidPrice", MidPrice);
                    break;
                case "PREMIUM":
                    where += " AND BasePrice > @MidPrice";
                    parameters.Add("MidPrice", MidPrice);
                    break;
            }
        }

        return (where, parameters);
    }
}
