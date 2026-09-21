namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

internal static class PlatformBranchSql
{
    internal const string SelectColumns = """
        SELECT b.Id, b.TenantId, b.ParentBranchId, b.BranchCode, b.Name,
               b.BranchType, b.BranchManagerUserId, u.FullName AS BranchManagerName,
               b.Phone, b.Email, b.Address, b.City, b.Country, b.TimeZone, b.CurrencyCode,
               b.Status, b.IsGpsEnabled, b.IsActive
        FROM Branches b
        LEFT JOIN Users u ON u.Id = b.BranchManagerUserId AND u.IsDeleted = 0
        """;
}
