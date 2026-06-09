using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence;

public class TenantModuleService(IDbConnectionFactory dbFactory) : ITenantModuleService
{
    public async Task<IReadOnlyList<string>> GetLegacyModuleKeysAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var codes = (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT m.ModuleCode
            FROM TenantModules tm
            INNER JOIN Modules m ON m.Id = tm.ModuleId
            WHERE tm.TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        if (codes.Count > 0)
            return TenantModuleCatalog.LegacyKeysFromCodes(codes);

        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT EnabledModulesJson FROM Tenants WHERE Id = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(json))
            return TenantModuleCatalog.LegacyKeysFromCodes(TenantModuleCatalog.DefaultModuleCodes);

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch
        {
            return TenantModuleCatalog.LegacyKeysFromCodes(TenantModuleCatalog.DefaultModuleCodes);
        }
    }

    public async Task SyncLegacyJsonAsync(int tenantId, IReadOnlyList<string> moduleCodes, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var legacyKeys = TenantModuleCatalog.LegacyKeysFromCodes(moduleCodes);
        var json = TenantModuleCatalog.SerializeLegacyKeys(legacyKeys);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Tenants SET EnabledModulesJson = @Json, UpdatedAt = GETUTCDATE() WHERE Id = @TenantId",
            new { TenantId = tenantId, Json = json }, cancellationToken: cancellationToken));
    }
}
