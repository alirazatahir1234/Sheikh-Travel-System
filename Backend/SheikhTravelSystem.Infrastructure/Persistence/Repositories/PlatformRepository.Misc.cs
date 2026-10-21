using Dapper;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    public async Task<IReadOnlyList<string>> GetBranchLabelsAsync(
        int tenantId, IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return Array.Empty<string>();
        using var connection = dbFactory.CreateConnection();
        var labels = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT Name FROM Branches
            WHERE TenantId = @TenantId AND Id IN @Ids
            ORDER BY Name
            """,
            new { TenantId = tenantId, Ids = ids.ToArray() },
            cancellationToken: cancellationToken));
        return labels.ToList();
    }

    public async Task<IReadOnlyList<string>> GetDepartmentLabelsAsync(
        int tenantId, IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return Array.Empty<string>();
        using var connection = dbFactory.CreateConnection();
        var labels = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT Name FROM Departments
            WHERE TenantId = @TenantId AND Id IN @Ids
            ORDER BY Name
            """,
            new { TenantId = tenantId, Ids = ids.ToArray() },
            cancellationToken: cancellationToken));
        return labels.ToList();
    }

    public async Task<int?> GetUserTenantIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Users WHERE Id = @Id AND IsDeleted = 0",
            new { Id = userId },
            cancellationToken: cancellationToken));
    }

}
