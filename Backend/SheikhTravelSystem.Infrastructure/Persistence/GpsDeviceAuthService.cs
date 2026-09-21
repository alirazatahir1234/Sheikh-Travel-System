using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence;

public sealed class GpsDeviceAuthService(IDbConnectionFactory dbFactory) : IGpsDeviceAuthService
{
    public async Task<int?> GetTenantIdByUniqueIdAsync(string uniqueId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 TenantId FROM GpsDevices
            WHERE UniqueId = @UniqueId AND IsDeleted = 0
            """,
            new { UniqueId = uniqueId.Trim() },
            cancellationToken: cancellationToken));
    }

    public async Task<string?> GetTenantGpsApiKeyAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT ApiKey FROM TenantGpsSettings WHERE TenantId = @TenantId",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    public async Task<string?> GetPlatformGpsDeviceApiKeyAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT TOP 1 Value FROM PlatformSettings
            WHERE TenantId = @TenantId AND Category = N'Integrations' AND [Key] = N'GpsDeviceApiKey'
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
    }
}
