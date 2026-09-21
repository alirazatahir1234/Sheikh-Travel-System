namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

/// <summary>
/// Public website SQL fragments — kept as named constants so tests can assert publish filters.
/// </summary>
public static class WebsitePublicSql
{
    public const string PublishedFeatures =
        "WHERE TenantId = @TenantId AND IsActive = 1 AND Status = N'Published' ORDER BY DisplayOrder, Id";

    public const string PageSections =
        "WHERE TenantId = @TenantId AND PageId = @PageId AND IsActive = 1 AND Status = N'Published' ORDER BY DisplayOrder, Id";
}
