using System.Text.Json;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    private const string TenantSelectSql = """
        SELECT t.Id, t.Name, t.Slug, t.Code, t.TenantType,
               COALESCE(tb.Country, (SELECT TOP 1 b.Country FROM Branches b WHERE b.TenantId = t.Id ORDER BY b.Id)) AS Country,
               COALESCE(s.PlanName, t.SubscriptionPlan) AS SubscriptionPlan, t.IsActive, t.CreatedAt,
               (SELECT COUNT(*) FROM Branches b WHERE b.TenantId = t.Id) AS BranchCount,
               (SELECT COUNT(*) FROM Departments d WHERE d.TenantId = t.Id) AS DepartmentCount,
               (SELECT COUNT(*) FROM Roles r WHERE r.TenantId = t.Id) AS RoleCount,
               (SELECT TOP 1
                    CASE WHEN b.City IS NOT NULL AND b.Country IS NOT NULL THEN b.City + N', ' + b.Country
                         WHEN b.City IS NOT NULL THEN b.City
                         WHEN b.Country IS NOT NULL THEN b.Country
                         ELSE NULL END
                FROM Branches b WHERE b.TenantId = t.Id ORDER BY b.Id) AS Location,
               (SELECT COUNT(*) FROM Users u WHERE u.TenantId = t.Id AND u.IsDeleted = 0 AND u.IsActive = 1) AS ActiveUserCount,
               s.MaxUsers,
               (SELECT COUNT(*) FROM Vehicles v WHERE v.TenantId = t.Id AND v.IsDeleted = 0) AS ActiveVehicleCount,
               s.MaxVehicles,
               (SELECT STRING_AGG(m.ModuleCode, N',') WITHIN GROUP (ORDER BY m.ModuleCode)
                FROM TenantModules tm
                INNER JOIN Modules m ON m.Id = tm.ModuleId
                WHERE tm.TenantId = t.Id) AS ModuleCodes,
               s.SubscriptionEndDate,
               s.Status AS SubscriptionStatus
        FROM Tenants t
        LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
        LEFT JOIN TenantBranding tb ON tb.TenantId = t.Id
        """;

    public async Task<IReadOnlyList<TenantListDto>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<TenantListDto>(new CommandDefinition(
            TenantSelectSql + " ORDER BY t.Name",
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TenantListDto>> GetTenantByScopeAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<TenantListDto>(new CommandDefinition(
            TenantSelectSql + " WHERE t.Id = @TenantId ORDER BY t.Name",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<TenantDetailDto?> GetTenantDetailRowAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantDetailDto>(new CommandDefinition("""
            SELECT t.Id, t.Name, t.Slug, t.Code, t.TenantType, t.IndustryType,
                   COALESCE(t.StorageModel, N'SharedDatabase') AS StorageModel,
                   COALESCE(t.Status, CASE WHEN t.IsActive = 1 THEN N'Active' ELSE N'Suspended' END) AS Status,
                   t.IsActive, t.DataRegion, t.CreatedAt, t.UpdatedAt,
                   COALESCE(s.PlanName, t.SubscriptionPlan) AS SubscriptionPlan,
                   s.MaxUsers, s.MaxVehicles, s.MaxDrivers, s.MaxBranches, s.MaxGpsDevices,
                   COALESCE(b.LogoUrl, t.LogoUrl) AS LogoUrl,
                   COALESCE(b.PrimaryColor, t.PrimaryColor) AS PrimaryColor,
                   b.Website, b.SupportEmail, b.Country, b.CurrencyCode, b.TimeZone,
                   (SELECT COUNT(*) FROM Branches br WHERE br.TenantId = t.Id) AS BranchCount,
                   (SELECT COUNT(*) FROM Departments d WHERE d.TenantId = t.Id) AS DepartmentCount,
                   (SELECT COUNT(*) FROM Roles r WHERE r.TenantId = t.Id) AS RoleCount,
                   (SELECT TOP 1
                        CASE WHEN br.City IS NOT NULL AND br.Country IS NOT NULL THEN br.City + N', ' + br.Country
                             WHEN br.City IS NOT NULL THEN br.City
                             WHEN br.Country IS NOT NULL THEN br.Country
                             ELSE NULL END
                    FROM Branches br WHERE br.TenantId = t.Id ORDER BY br.Id) AS Location
            FROM Tenants t
            LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
            LEFT JOIN TenantBranding b ON b.TenantId = t.Id
            WHERE t.Id = @Id
            """, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<string>> GetTenantModuleCodesOrderedAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var moduleCodes = (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT m.ModuleCode
            FROM TenantModules tm
            INNER JOIN Modules m ON m.Id = tm.ModuleId
            WHERE tm.TenantId = @Id
            ORDER BY m.ModuleCode
            """, new { Id = tenantId }, cancellationToken: cancellationToken))).ToList();
        return moduleCodes;
    }

    public async Task<TenantAdminInfoDto?> GetTenantAdminInfoAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantAdminInfoDto>(new CommandDefinition("""
            SELECT TOP 1 Id, FullName, Email, Phone,
                   IsActive,
                   CASE WHEN IsActive = 1 THEN N'Active' ELSE N'Disabled' END AS Status
            FROM Users
            WHERE TenantId = @Id AND Role = 1 AND IsDeleted = 0
            ORDER BY Id
            """, new { Id = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<string>> GetLegacyModuleKeysAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT EnabledModulesJson FROM Tenants WHERE Id = @Id",
            new { Id = tenantId }, cancellationToken: cancellationToken));
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    public async Task<int> ResetTenantAdminPasswordAsync(int tenantId, string passwordHash, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Users SET PasswordHash = @Hash
            WHERE TenantId = @TenantId AND Role = 1 AND IsDeleted = 0
            """, new { Hash = passwordHash, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<TenantManagementStatsDto> GetTenantManagementStatsGlobalAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleAsync<TenantManagementStatsDto>(new CommandDefinition("""
                SELECT
                    (SELECT COUNT(*) FROM Tenants WHERE IsActive = 1) AS ActiveTenants,
                    (SELECT COUNT(*) FROM Users WHERE IsDeleted = 0 AND IsActive = 1) AS ActiveUsers,
                    (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0) AS ActiveVehicles,
                    (SELECT COUNT(*) FROM TenantSubscriptions
                     WHERE SubscriptionEndDate IS NOT NULL
                       AND SubscriptionEndDate >= GETUTCDATE()
                       AND SubscriptionEndDate <= DATEADD(day, 30, GETUTCDATE())) AS ExpiringPlans,
                    (SELECT CAST(ISNULL(SUM(
                        CASE
                            WHEN LOWER(COALESCE(s.PlanName, t.SubscriptionPlan, N'')) LIKE N'%enterprise%' THEN 25000
                            WHEN LOWER(COALESCE(s.PlanName, t.SubscriptionPlan, N'')) LIKE N'%pro%' THEN 15000
                            ELSE 5000
                        END), 0) AS DECIMAL(18,2))
                     FROM Tenants t
                     LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
                     WHERE t.IsActive = 1) AS MonthlyRevenue,
                    (SELECT COUNT(*) FROM Tenants
                     WHERE CreatedAt >= DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1)) AS TenantsAddedThisMonth
                """, cancellationToken: cancellationToken));
    }

    public async Task<TenantManagementStatsDto> GetTenantManagementStatsScopedAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleAsync<TenantManagementStatsDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM Tenants WHERE Id = @TenantId AND IsActive = 1) AS ActiveTenants,
                (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1) AS ActiveUsers,
                (SELECT COUNT(*) FROM Vehicles WHERE TenantId = @TenantId AND IsDeleted = 0) AS ActiveVehicles,
                (SELECT COUNT(*) FROM TenantSubscriptions
                 WHERE TenantId = @TenantId
                   AND SubscriptionEndDate IS NOT NULL
                   AND SubscriptionEndDate >= GETUTCDATE()
                   AND SubscriptionEndDate <= DATEADD(day, 30, GETUTCDATE())) AS ExpiringPlans,
                (SELECT CAST(ISNULL(SUM(
                    CASE
                        WHEN LOWER(COALESCE(s.PlanName, t.SubscriptionPlan, N'')) LIKE N'%enterprise%' THEN 25000
                        WHEN LOWER(COALESCE(s.PlanName, t.SubscriptionPlan, N'')) LIKE N'%pro%' THEN 15000
                        ELSE 5000
                    END), 0) AS DECIMAL(18,2))
                 FROM Tenants t
                 LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
                 WHERE t.Id = @TenantId AND t.IsActive = 1) AS MonthlyRevenue,
                0 AS TenantsAddedThisMonth
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task UpdateTenantAsync(
        int id, string name, string? subscriptionPlan, bool isActive,
        int? maxUsers, int? maxVehicles, int? maxDrivers, int? maxBranches, int? maxGpsDevices,
        IReadOnlyList<string>? moduleCodes, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Tenants
            SET Name = @Name, SubscriptionPlan = @SubscriptionPlan, IsActive = @IsActive,
                Status = CASE WHEN @IsActive = 1 THEN N'Active' ELSE N'Suspended' END,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id
            """, new
        {
            Id = id,
            Name = name,
            SubscriptionPlan = subscriptionPlan,
            IsActive = isActive
        }, cancellationToken: cancellationToken));

        if (affected == 0) throw new NotFoundException("Tenant", id);

        if (!string.IsNullOrWhiteSpace(subscriptionPlan)
            || maxUsers.HasValue
            || maxVehicles.HasValue
            || maxDrivers.HasValue
            || maxBranches.HasValue
            || maxGpsDevices.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM TenantSubscriptions WHERE TenantId = @Id)
                    UPDATE TenantSubscriptions
                    SET PlanName = COALESCE(@PlanName, PlanName),
                        MaxUsers = COALESCE(@MaxUsers, MaxUsers),
                        MaxVehicles = COALESCE(@MaxVehicles, MaxVehicles),
                        MaxDrivers = COALESCE(@MaxDrivers, MaxDrivers),
                        MaxBranches = COALESCE(@MaxBranches, MaxBranches),
                        MaxGpsDevices = COALESCE(@MaxGpsDevices, MaxGpsDevices),
                        UpdatedAt = GETUTCDATE()
                    WHERE TenantId = @Id;
                ELSE
                    INSERT INTO TenantSubscriptions (TenantId, PlanName, MaxUsers, MaxVehicles, MaxDrivers, MaxBranches, MaxGpsDevices, Status, SubscriptionStartDate)
                    VALUES (@Id, COALESCE(@PlanName, N'Enterprise'), @MaxUsers, @MaxVehicles, @MaxDrivers, @MaxBranches, @MaxGpsDevices, N'Active', GETUTCDATE());
                """, new
            {
                Id = id,
                PlanName = subscriptionPlan,
                MaxUsers = maxUsers,
                MaxVehicles = maxVehicles,
                MaxDrivers = maxDrivers,
                MaxBranches = maxBranches,
                MaxGpsDevices = maxGpsDevices
            }, cancellationToken: cancellationToken));
        }

        if (moduleCodes is { Count: > 0 })
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM TenantModules WHERE TenantId = @Id",
                new { Id = id }, cancellationToken: cancellationToken));

            foreach (var code in moduleCodes)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO TenantModules (TenantId, ModuleId)
                    SELECT @Id, m.Id FROM Modules m WHERE m.ModuleCode = @Code;
                    """, new { Id = id, Code = code }, cancellationToken: cancellationToken));
            }
        }
    }

    public async Task<(string? DefaultWorkspaceKey, string? HomeRoute, string? RoleCode)?> GetUserWorkspaceProfileAsync(
        int userId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<(string? DefaultWorkspaceKey, string? HomeRoute, string? RoleCode)>(
            new CommandDefinition("""
                SELECT u.DefaultWorkspaceKey, u.HomeRoute,
                       (SELECT TOP 1 r.Code
                        FROM UserRoles ur INNER JOIN Roles r ON r.Id = ur.RoleId
                        WHERE ur.UserId = u.Id ORDER BY r.Id) AS RoleCode
                FROM Users u
                WHERE u.Id = @UserId AND u.TenantId = @TenantId AND u.IsDeleted = 0
                """,
                new { UserId = userId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<(string? DefaultDashboardKey, string? DefaultWorkspaceKey, string? RoleCode)?> GetUserDashboardProfileAsync(
        int userId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<(
            string? DefaultDashboardKey, string? DefaultWorkspaceKey, string? RoleCode)>(new CommandDefinition("""
            SELECT u.DefaultDashboardKey, u.DefaultWorkspaceKey,
                   (SELECT TOP 1 r.Code FROM UserRoles ur
                    INNER JOIN Roles r ON r.Id = ur.RoleId
                    WHERE ur.UserId = u.Id ORDER BY r.Id) AS RoleCode
            FROM Users u
            WHERE u.Id = @UserId AND u.TenantId = @TenantId AND u.IsDeleted = 0
            """,
            new { UserId = userId, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }
}
