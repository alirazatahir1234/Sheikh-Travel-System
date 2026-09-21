using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence;

public sealed class TenantLookupService(IDbConnectionFactory dbFactory) : ITenantLookupService
{
    public async Task<int?> GetTenantIdBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Id FROM Tenants WHERE Slug = @Slug AND IsActive = 1",
                new { Slug = slug },
                cancellationToken: cancellationToken));
    }

    public async Task<int?> GetTenantIdByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT TenantId FROM Users WHERE Id = @UserId AND IsDeleted = 0",
                new { UserId = userId },
                cancellationToken: cancellationToken));
    }
}
