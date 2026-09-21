using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    public async Task<IReadOnlyList<PermissionRowData>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        List<PermissionRowData> rows;
        try
        {
            rows = (await connection.QueryAsync<PermissionRowData>(new CommandDefinition("""
                SELECT Id, ModuleName, PermissionCode, Description,
                       COALESCE(DisplayName, PermissionCode) AS DisplayName,
                       Category, COALESCE(SortOrder, 0) AS SortOrder,
                       CONVERT(bit, COALESCE(Visible, 1)) AS Visible, Action, ModuleKey
                FROM Permissions
                ORDER BY COALESCE(SortOrder, 0), ModuleName, PermissionCode
                """, cancellationToken: cancellationToken))).ToList();
        }
        catch
        {
            rows = (await connection.QueryAsync<PermissionRowData>(new CommandDefinition("""
                SELECT Id, ModuleName, PermissionCode, Description,
                       PermissionCode AS DisplayName,
                       CAST(NULL AS nvarchar(100)) AS Category,
                       0 AS SortOrder,
                       CONVERT(bit, 1) AS Visible,
                       CAST(NULL AS nvarchar(50)) AS Action,
                       CAST(NULL AS nvarchar(100)) AS ModuleKey
                FROM Permissions
                ORDER BY ModuleName, PermissionCode
                """, cancellationToken: cancellationToken))).ToList();
        }
        return rows;
    }

    public async Task<IReadOnlyList<RoleSummaryDto>> LoadRoleSummariesAsync(
        int tenantId, bool visibleOnly = false, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        List<(int Id, int TenantId, string Name, string Code, bool IsSystem, bool IsActive,
            string? DisplayName, string? Description, string? Category, string? RoleType, int SortOrder, bool Visible)> roles;
        try
        {
            var sql = """
                SELECT Id, TenantId, Name, Code, IsSystem, IsActive,
                       COALESCE(DisplayName, Name) AS DisplayName, Description, Category,
                       COALESCE(RoleType, CASE WHEN IsSystem = 1 THEN N'System' ELSE N'Custom' END) AS RoleType,
                       COALESCE(SortOrder, 0) AS SortOrder,
                       COALESCE(Visible, 1) AS Visible
                FROM Roles WHERE TenantId = @TenantId
                """ + (visibleOnly ? " AND COALESCE(Visible, 1) = 1 AND IsActive = 1" : "") + """
                ORDER BY COALESCE(SortOrder, 0), Name
                """;
            roles = (await connection.QueryAsync<(int Id, int TenantId, string Name, string Code, bool IsSystem, bool IsActive,
                string? DisplayName, string? Description, string? Category, string? RoleType, int SortOrder, bool Visible)>(
                new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        }
        catch
        {
            roles = (await connection.QueryAsync<(int Id, int TenantId, string Name, string Code, bool IsSystem, bool IsActive,
                string? DisplayName, string? Description, string? Category, string? RoleType, int SortOrder, bool Visible)>(
                new CommandDefinition(
                    """
                    SELECT Id, TenantId, Name, Code, IsSystem, IsActive,
                           Name AS DisplayName, CAST(NULL AS NVARCHAR(500)) AS Description,
                           CAST(NULL AS NVARCHAR(100)) AS Category,
                           CASE WHEN IsSystem = 1 THEN N'System' ELSE N'Custom' END AS RoleType,
                           0 AS SortOrder, CAST(1 AS bit) AS Visible
                    FROM Roles WHERE TenantId = @TenantId ORDER BY Name
                    """,
                    new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        }

        if (roles.Count == 0) return Array.Empty<RoleSummaryDto>();

        var roleIds = roles.Select(r => r.Id).ToArray();
        var userCounts = (await connection.QueryAsync<(int RoleId, int UserCount)>(new CommandDefinition("""
            SELECT ur.RoleId, COUNT(*) AS UserCount
            FROM UserRoles ur
            INNER JOIN Users u ON u.Id = ur.UserId AND u.IsDeleted = 0
            WHERE ur.RoleId IN @RoleIds
            GROUP BY ur.RoleId
            """, new { RoleIds = roleIds }, cancellationToken: cancellationToken)))
            .ToDictionary(x => x.RoleId, x => x.UserCount);

        var permMap = new Dictionary<int, List<string>>();
        var pairs = await connection.QueryAsync<(int RoleId, string PermissionCode)>(new CommandDefinition(
            @"SELECT rp.RoleId, p.PermissionCode
              FROM RolePermissions rp
              INNER JOIN Permissions p ON p.Id = rp.PermissionId
              WHERE rp.RoleId IN @RoleIds",
            new { RoleIds = roleIds }, cancellationToken: cancellationToken));

        foreach (var pair in pairs)
        {
            if (!permMap.TryGetValue(pair.RoleId, out var list))
            {
                list = [];
                permMap[pair.RoleId] = list;
            }
            list.Add(pair.PermissionCode);
        }

        return roles.Select(r =>
        {
            var seed = RoleRegistrySeed.Find(r.Code);
            var perms = permMap.TryGetValue(r.Id, out var list) ? list : [];
            return new RoleSummaryDto(
                r.Id, r.TenantId, r.Name, r.Code, r.IsSystem, r.IsActive,
                userCounts.GetValueOrDefault(r.Id),
                perms.Count,
                perms,
                r.DisplayName ?? seed?.DisplayName ?? r.Name,
                r.Description ?? seed?.Description,
                r.Category ?? seed?.Category,
                r.RoleType ?? seed?.RoleType ?? (r.IsSystem ? "System" : "Custom"),
                r.SortOrder != 0 ? r.SortOrder : (seed?.SortOrder ?? 0),
                r.Visible);
        }).ToList();
    }

    public async Task<bool> RoleCodeExistsAsync(int tenantId, string code, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Roles WHERE TenantId = @TenantId AND Code = @Code) THEN 1 ELSE 0 END",
            new { TenantId = tenantId, Code = code }, cancellationToken: cancellationToken));
    }

    public async Task<int> CreateRoleAsync(int tenantId, string name, string code, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
            VALUES (@TenantId, @Name, @Code, 0, 1, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new { TenantId = tenantId, Name = name, Code = code }, cancellationToken: cancellationToken));
    }

    public async Task<int> CreateRoleForTenantAsync(int tenantId, string name, string code, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt,
                    DisplayName, Description, Category, SortOrder, Visible, RoleType)
                VALUES (@TenantId, @Name, @Code, 0, 1, GETUTCDATE(),
                    @DisplayName, NULL, N'Custom', 100, 1, N'Custom');
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
            {
                TenantId = tenantId,
                Name = name,
                Code = code,
                DisplayName = name
            }, cancellationToken: cancellationToken));
        }
        catch
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
                VALUES (@TenantId, @Name, @Code, 0, 1, GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new { TenantId = tenantId, Name = name, Code = code },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<(bool IsSystem, string? Code)?> GetRoleMetaAsync(int tenantId, int roleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var role = await connection.QuerySingleOrDefaultAsync<(bool IsSystem, string? Code)>(new CommandDefinition(
            "SELECT IsSystem, Code FROM Roles WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = roleId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (role.Code is null) return null;
        return role;
    }

    public async Task UpdateRoleForTenantAsync(
        int tenantId, int roleId, string name, bool isActive, string displayName, string? description, string? category,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Roles SET
                    Name = @Name,
                    IsActive = @IsActive,
                    DisplayName = @DisplayName,
                    Description = @Description,
                    Category = @Category,
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND TenantId = @TenantId
                """, new
            {
                Id = roleId,
                TenantId = tenantId,
                Name = name,
                IsActive = isActive,
                DisplayName = displayName,
                Description = description,
                Category = category
            }, cancellationToken: cancellationToken));

            if (affected == 0) throw new NotFoundException("Role", roleId);
        }
        catch (NotFoundException)
        {
            throw;
        }
        catch
        {
            var affected = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Roles SET Name = @Name, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND TenantId = @TenantId
                """, new
            {
                Id = roleId,
                TenantId = tenantId,
                Name = name,
                IsActive = isActive
            }, cancellationToken: cancellationToken));
            if (affected == 0) throw new NotFoundException("Role", roleId);
        }
    }

    public async Task<int> CountUsersWithRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM UserRoles ur
            INNER JOIN Users u ON u.Id = ur.UserId AND u.IsDeleted = 0
            WHERE ur.RoleId = @RoleId
            """, new { RoleId = roleId }, cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteRoleAsync(int tenantId, int roleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Roles WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = roleId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> RoleExistsAsync(int tenantId, int roleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Roles WHERE Id = @RoleId AND TenantId = @TenantId) THEN 1 ELSE 0 END",
            new { RoleId = roleId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task ReplaceRolePermissionsAsync(int roleId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM RolePermissions WHERE RoleId = @RoleId",
            new { RoleId = roleId }, cancellationToken: cancellationToken));

        foreach (var code in permissionCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT @RoleId, p.Id FROM Permissions p WHERE p.PermissionCode = @Code
                """, new { RoleId = roleId, Code = code }, cancellationToken: cancellationToken));
        }
    }

    public async Task ApplyRoleTemplateAsync(
        int tenantId, string code, string name, RoleRegistrySeed.Entry? seed, IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var roleId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Id FROM Roles WHERE TenantId = @TenantId AND Code = @Code",
            new { TenantId = tenantId, Code = code }, cancellationToken: cancellationToken));

        if (!roleId.HasValue)
        {
            try
            {
                roleId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                    INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt,
                        DisplayName, Description, Category, SortOrder, Visible, RoleType)
                    VALUES (@TenantId, @Name, @Code, 1, 1, GETUTCDATE(),
                        @DisplayName, @Description, @Category, @SortOrder, 1, N'System');
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """, new
                {
                    TenantId = tenantId,
                    Name = name,
                    Code = code,
                    DisplayName = seed?.DisplayName ?? name,
                    Description = seed?.Description,
                    Category = seed?.Category,
                    SortOrder = seed?.SortOrder ?? 0
                }, cancellationToken: cancellationToken));
            }
            catch
            {
                roleId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                    INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
                    VALUES (@TenantId, @Name, @Code, 1, 1, GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """, new { TenantId = tenantId, Name = name, Code = code },
                    cancellationToken: cancellationToken));
            }
        }
        else if (seed is not null)
        {
            try
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE Roles SET
                        DisplayName = COALESCE(DisplayName, @DisplayName),
                        Description = COALESCE(Description, @Description),
                        Category = COALESCE(Category, @Category),
                        SortOrder = CASE WHEN SortOrder = 0 THEN @SortOrder ELSE SortOrder END,
                        RoleType = N'System',
                        Visible = 1
                    WHERE Id = @RoleId
                    """, new
                {
                    RoleId = roleId.Value,
                    DisplayName = seed.DisplayName,
                    Description = seed.Description,
                    Category = seed.Category,
                    SortOrder = seed.SortOrder
                }, cancellationToken: cancellationToken));
            }
            catch
            {
                // Metadata columns may not exist yet.
            }
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM RolePermissions WHERE RoleId = @RoleId",
            new { RoleId = roleId.Value }, cancellationToken: cancellationToken));

        foreach (var perm in permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT @RoleId, p.Id FROM Permissions p WHERE p.PermissionCode = @Code
                """, new { RoleId = roleId.Value, Code = perm }, cancellationToken: cancellationToken));
        }
    }

    public async Task<TenantSecuritySettingsDto?> GetTenantSecuritySettingsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantSecuritySettingsDto>(new CommandDefinition("""
            SELECT IsMfaRequired, PasswordExpiryDays, SessionTimeoutMinutes,
                   IsGdprEnabled, IsAuditLoggingEnabled, IsVatEnabled
            FROM TenantSecuritySettings WHERE TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task UpsertTenantSecuritySettingsAsync(int tenantId, TenantSecuritySettingsDto p, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantSecuritySettings')
            BEGIN
                IF EXISTS (SELECT 1 FROM TenantSecuritySettings WHERE TenantId = @TenantId)
                    UPDATE TenantSecuritySettings SET
                        IsMfaRequired = @IsMfaRequired,
                        PasswordExpiryDays = @PasswordExpiryDays,
                        SessionTimeoutMinutes = @SessionTimeoutMinutes,
                        IsGdprEnabled = @IsGdprEnabled,
                        IsAuditLoggingEnabled = @IsAuditLoggingEnabled,
                        IsVatEnabled = @IsVatEnabled
                    WHERE TenantId = @TenantId;
                ELSE
                    INSERT INTO TenantSecuritySettings (TenantId, IsMfaRequired, PasswordExpiryDays, SessionTimeoutMinutes,
                        IsGdprEnabled, IsAuditLoggingEnabled, IsVatEnabled)
                    VALUES (@TenantId, @IsMfaRequired, @PasswordExpiryDays, @SessionTimeoutMinutes,
                        @IsGdprEnabled, @IsAuditLoggingEnabled, @IsVatEnabled);
            END
            """, new
        {
            TenantId = tenantId,
            p.IsMfaRequired,
            p.PasswordExpiryDays,
            p.SessionTimeoutMinutes,
            p.IsGdprEnabled,
            p.IsAuditLoggingEnabled,
            p.IsVatEnabled
        }, cancellationToken: cancellationToken));
    }

    public async Task<DateTime?> GetUserPasswordChangedAtAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            "SELECT PasswordChangedAt FROM Users WHERE Id = @Id",
            new { Id = userId },
            cancellationToken: cancellationToken));
    }
}
