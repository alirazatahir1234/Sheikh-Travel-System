using System.Text.Json;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    private const string ModuleSelectSql = """
        SELECT Id, ModuleCode AS Code, ModuleName AS Name,
               COALESCE(DisplayName, ModuleName) AS DisplayName,
               Description, Category,
               COALESCE(Version, N'1.0.0') AS Version,
               Icon, Route, COALESCE(SortOrder, 0) AS SortOrder,
               DependenciesJson, COALESCE(Visible, 1) AS Visible,
               COALESCE(IsMobileSupported, 0) AS IsMobileSupported,
               COALESCE(IsAISupported, 0) AS IsAISupported,
               COALESCE(IsGPSSupported, 0) AS IsGPSSupported,
               COALESCE(Status, N'Active') AS Status,
               DocumentationUrl, LegacyKeysJson
        FROM Modules
        """;

    public async Task<IReadOnlyList<ModuleRegistryDto>> LoadModuleCatalogAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            var rows = (await connection.QueryAsync<ModuleRegistryQueries.ModuleRow>(new CommandDefinition(
                ModuleSelectSql + " WHERE COALESCE(Visible, 1) = 1 ORDER BY COALESCE(SortOrder, 0), ModuleCode",
                cancellationToken: cancellationToken))).ToList();

            if (rows.Count > 0)
                return rows.Select(r => ModuleRegistryQueries.ToRegistryDto(r)).ToList();
        }
        catch
        {
            // Metadata columns / table may not exist yet.
        }

        return ModuleRegistrySeed.All
            .Where(e => e.Visible)
            .OrderBy(e => e.SortOrder)
            .Select(e => ModuleRegistryQueries.FromSeed(e))
            .ToList();
    }

    public async Task<ModuleRegistryDto?> LoadModuleByKeyAsync(string codeOrId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            ModuleRegistryQueries.ModuleRow? row = null;
            if (int.TryParse(codeOrId, out var id))
            {
                row = await connection.QuerySingleOrDefaultAsync<ModuleRegistryQueries.ModuleRow>(new CommandDefinition(
                    ModuleSelectSql + " WHERE Id = @Id",
                    new { Id = id },
                    cancellationToken: cancellationToken));
            }

            row ??= await connection.QuerySingleOrDefaultAsync<ModuleRegistryQueries.ModuleRow>(new CommandDefinition(
                ModuleSelectSql + " WHERE ModuleCode = @Code",
                new { Code = codeOrId },
                cancellationToken: cancellationToken));

            if (row is not null)
                return ModuleRegistryQueries.ToRegistryDto(row);
        }
        catch
        {
            // fall through to seed
        }

        var seed = ModuleRegistrySeed.All.FirstOrDefault(e =>
            string.Equals(e.Code, codeOrId, StringComparison.OrdinalIgnoreCase));
        return seed is null ? null : ModuleRegistryQueries.FromSeed(seed);
    }

    public async Task<IReadOnlyList<string>> GetInstalledModuleCodesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var codes = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT m.ModuleCode
            FROM TenantModules tm
            INNER JOIN Modules m ON m.Id = tm.ModuleId
            WHERE tm.TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return codes.ToList();
    }

    public async Task<(string Name, string? PlanName)?> GetTenantNameAndPlanAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenant = await connection.QuerySingleOrDefaultAsync<(string Name, string? PlanName)>(new CommandDefinition("""
            SELECT t.Name, COALESCE(s.PlanName, t.SubscriptionPlan) AS PlanName
            FROM Tenants t
            LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
            WHERE t.Id = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        if (tenant.Name is null) return null;
        return tenant;
    }

    public async Task<string?> GetEnabledModulesJsonAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT EnabledModulesJson FROM Tenants WHERE Id = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<TenantModuleLimitsRow> GetTenantModuleLimitsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var limits = await connection.QuerySingleAsync<(int? MaxUsers, int? MaxVehicles, int? MaxDrivers, int? MaxBranches, int? MaxGpsDevices,
            int UsedUsers, int UsedVehicles, int UsedDrivers, int UsedBranches, int UsedGps)>(new CommandDefinition("""
            SELECT
                s.MaxUsers, s.MaxVehicles, s.MaxDrivers, s.MaxBranches, s.MaxGpsDevices,
                (SELECT COUNT(*) FROM Users u WHERE u.TenantId = @TenantId AND u.IsDeleted = 0 AND u.IsActive = 1) AS UsedUsers,
                (SELECT COUNT(*) FROM Vehicles v WHERE v.TenantId = @TenantId AND v.IsDeleted = 0) AS UsedVehicles,
                (SELECT COUNT(*) FROM Drivers d WHERE d.TenantId = @TenantId AND d.IsDeleted = 0) AS UsedDrivers,
                (SELECT COUNT(*) FROM Branches b WHERE b.TenantId = @TenantId) AS UsedBranches,
                (SELECT COUNT(*) FROM GpsDevices g
                 INNER JOIN Vehicles v ON v.Id = g.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
                 WHERE g.IsDeleted = 0) AS UsedGps
            FROM (SELECT 1 AS x) seed
            LEFT JOIN TenantSubscriptions s ON s.TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return new TenantModuleLimitsRow(
            limits.MaxUsers, limits.MaxVehicles, limits.MaxDrivers, limits.MaxBranches, limits.MaxGpsDevices,
            limits.UsedUsers, limits.UsedVehicles, limits.UsedDrivers, limits.UsedBranches, limits.UsedGps);
    }

    public async Task EnsureTenantExistsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Tenants WHERE Id = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        if (tenantExists == 0) throw new NotFoundException("Tenant", tenantId);
    }

    public async Task ReplaceTenantModulesAsync(int tenantId, IReadOnlyList<string> moduleCodes, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM TenantModules WHERE TenantId = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));

        foreach (var code in moduleCodes)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TenantModules (TenantId, ModuleId)
                SELECT @TenantId, m.Id FROM Modules m WHERE m.ModuleCode = @Code;
                """, new { TenantId = tenantId, Code = code }, cancellationToken: cancellationToken));
        }
    }
}
