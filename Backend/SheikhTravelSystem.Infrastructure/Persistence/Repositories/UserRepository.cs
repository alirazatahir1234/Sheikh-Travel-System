using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Users.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IDbConnectionFactory dbFactory) : IUserRepository
{
    private const string SelectSql = """
        SELECT u.Id, u.FullName, u.Email, u.Phone, u.Role, u.IsActive, u.CreatedAt,
               u.TenantId AS CompanyId, t.Name AS CompanyName,
               u.BranchId, br.Name AS BranchName,
               u.DepartmentId, d.Name AS DepartmentName,
               u.JobTitle, u.EmployeeCode, u.EmployeeType,
               COALESCE(u.Status, CASE WHEN u.IsActive = 1 THEN N'Active' ELSE N'Inactive' END) AS Status,
               u.DefaultWorkspaceKey, u.DefaultDashboardKey, u.HomeRoute,
               u.TimeZone, u.Language, u.Theme, u.AvatarUrl,
               up.LastLoginAt
        FROM Users u
        LEFT JOIN Tenants t ON t.Id = u.TenantId
        LEFT JOIN Branches br ON br.Id = u.BranchId
        LEFT JOIN Departments d ON d.Id = u.DepartmentId
        LEFT JOIN UserPresence up ON up.UserId = u.Id
        """;

    public async Task<(IReadOnlyList<UserDto> Items, int TotalCount)> GetPagedAsync(
        int tenantId,
        int page,
        int pageSize,
        int? branchId,
        int? departmentId,
        string? status,
        string? employeeType,
        string? search,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var where = """
            WHERE u.IsDeleted = 0 AND u.TenantId = @TenantId
            """;
        if (branchId.HasValue)
            where += " AND u.BranchId = @BranchId";
        if (departmentId.HasValue)
            where += " AND u.DepartmentId = @DepartmentId";
        if (!string.IsNullOrWhiteSpace(status))
            where += " AND COALESCE(u.Status, CASE WHEN u.IsActive = 1 THEN N'Active' ELSE N'Inactive' END) = @Status";
        if (!string.IsNullOrWhiteSpace(employeeType))
            where += " AND u.EmployeeType = @EmployeeType";
        if (!string.IsNullOrWhiteSpace(search))
            where += """
                 AND (
                    u.FullName LIKE @Search OR u.Email LIKE @Search OR u.Phone LIKE @Search
                    OR u.EmployeeCode LIKE @Search OR u.JobTitle LIKE @Search
                 )
                """;

        var searchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
        var args = new
        {
            TenantId = tenantId,
            BranchId = branchId,
            DepartmentId = departmentId,
            Status = string.IsNullOrWhiteSpace(status) ? null : UserLifecycle.Normalize(status),
            EmployeeType = EmployeeTypes.Normalize(employeeType),
            Search = searchPattern,
            Offset = offset,
            PageSize = pageSize
        };

        try
        {
            var enrichedSql = SelectSql + "\n" + where + """

                        ORDER BY u.CreatedAt DESC
                        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                        """;
            var rows = (await connection.QueryAsync<UserRow>(
                new CommandDefinition(
                    enrichedSql,
                    args,
                    cancellationToken: cancellationToken))).ToList();

            var totalCount = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM Users u\n" + where,
                    args,
                    cancellationToken: cancellationToken));

            var roleMap = await LoadAssignedRolesMapAsync(
                connection, rows.Select(r => r.Id).ToList(), cancellationToken);

            return (
                rows.Select(r => ToDto(
                    r, roleMap.TryGetValue(r.Id, out var roles) ? roles : null)).ToList(),
                totalCount);
        }
        catch (Exception)
        {
            // Columns / joins may be incomplete — fall back to legacy select via row type
            // (Dapper cannot materialize UserDto records with optional ctor params).
            var rows = (await connection.QueryAsync<UserRow>(
                new CommandDefinition(
                    """
                    SELECT Id, FullName, Email, Phone, Role, IsActive, CreatedAt,
                           TenantId AS CompanyId,
                           CAST(NULL AS nvarchar(200)) AS CompanyName,
                           CAST(NULL AS int) AS BranchId,
                           CAST(NULL AS nvarchar(200)) AS BranchName,
                           CAST(NULL AS int) AS DepartmentId,
                           CAST(NULL AS nvarchar(200)) AS DepartmentName,
                           CAST(NULL AS nvarchar(200)) AS JobTitle,
                           CAST(NULL AS nvarchar(50)) AS EmployeeCode,
                           CAST(NULL AS nvarchar(50)) AS EmployeeType,
                           CASE WHEN IsActive = 1 THEN N'Active' ELSE N'Inactive' END AS Status,
                           CAST(NULL AS nvarchar(100)) AS DefaultWorkspaceKey,
                           CAST(NULL AS nvarchar(100)) AS DefaultDashboardKey,
                           CAST(NULL AS nvarchar(200)) AS HomeRoute,
                           CAST(NULL AS nvarchar(100)) AS TimeZone,
                           CAST(NULL AS nvarchar(20)) AS Language,
                           CAST(NULL AS nvarchar(50)) AS Theme,
                           CAST(NULL AS nvarchar(500)) AS AvatarUrl,
                           CAST(NULL AS datetime2) AS LastLoginAt
                    FROM Users
                    WHERE IsDeleted = 0 AND TenantId = @TenantId
                    ORDER BY CreatedAt DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                    """,
                    new { TenantId = tenantId, Offset = offset, PageSize = pageSize },
                    cancellationToken: cancellationToken))).ToList();

            var totalCount = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM Users WHERE IsDeleted = 0 AND TenantId = @TenantId",
                    new { TenantId = tenantId },
                    cancellationToken: cancellationToken));

            return (rows.Select(r => ToDto(r)).ToList(), totalCount);
        }
    }

    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        UserRow? row;
        try
        {
            row = await connection.QuerySingleOrDefaultAsync<UserRow>(
                new CommandDefinition(
                    SelectSql + " WHERE u.Id = @Id AND u.IsDeleted = 0",
                    new { Id = id },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            row = await connection.QuerySingleOrDefaultAsync<UserRow>(
                new CommandDefinition(
                    @"SELECT Id, FullName, Email, Phone, Role, IsActive, CreatedAt,
                             TenantId AS CompanyId,
                             CAST(NULL AS NVARCHAR(200)) AS CompanyName,
                             BranchId, CAST(NULL AS NVARCHAR(200)) AS BranchName,
                             DepartmentId, CAST(NULL AS NVARCHAR(200)) AS DepartmentName
                      FROM Users WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = id },
                    cancellationToken: cancellationToken));
        }

        if (row is null)
            return null;

        var roles = await LoadAssignedAsync(connection, id, cancellationToken);
        return ToDto(row, roles.Select(ToAssignedRoleDto).ToList());
    }

    public async Task<UserProfileDto?> GetProfileAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(
            new CommandDefinition(
                SelectSql + " WHERE u.Id = @Id AND u.IsDeleted = 0",
                new { Id = userId },
                cancellationToken: cancellationToken));

        if (row is null)
            return null;

        var roles = await LoadAssignedAsync(connection, userId, cancellationToken);
        return ToProfileDto(row, roles.Select(ToAssignedRoleDto).ToList());
    }

    public async Task<CompanyUserSummaryDto> GetCompanyUserSummaryAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            var row = await connection.QuerySingleAsync<(
                int TotalUsers, int Drivers, int Managers, int Administrators, int Staff, int DepartmentCount)>(
                new CommandDefinition("""
                    SELECT
                        (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0) AS TotalUsers,
                        (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0
                            AND EmployeeType = N'Driver') AS Drivers,
                        (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0
                            AND EmployeeType = N'Manager') AS Managers,
                        (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0
                            AND EmployeeType = N'Admin') AS Administrators,
                        (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0
                            AND EmployeeType = N'Staff') AS Staff,
                        (SELECT COUNT(*) FROM Departments WHERE TenantId = @TenantId) AS DepartmentCount
                    """,
                    new { TenantId = tenantId },
                    cancellationToken: cancellationToken));

            return new CompanyUserSummaryDto(
                tenantId,
                row.TotalUsers,
                row.Drivers,
                row.Managers,
                row.Administrators,
                row.Staff,
                row.DepartmentCount);
        }
        catch
        {
            var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
            var depts = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Departments WHERE TenantId = @TenantId",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
            return new CompanyUserSummaryDto(tenantId, total, 0, 0, 0, 0, depts);
        }
    }

    public async Task<int?> GetTenantIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT TenantId FROM Users WHERE Id = @Id AND IsDeleted = 0",
                new { Id = userId },
                cancellationToken: cancellationToken));
    }

    public async Task<UserOrgInfo?> GetOrgInfoAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var user = await connection.QuerySingleOrDefaultAsync<(int? TenantId, int? BranchId, int? DepartmentId)>(
            new CommandDefinition(
                "SELECT TenantId, BranchId, DepartmentId FROM Users WHERE Id = @Id AND IsDeleted = 0",
                new { Id = userId },
                cancellationToken: cancellationToken));

        if (user.TenantId is not int tenantId)
            return null;

        return new UserOrgInfo(tenantId, user.BranchId, user.DepartmentId);
    }

    public async Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Id = @UserId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { UserId = userId },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> EmailExistsInTenantAsync(
        string email,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Email = @Email AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Email = email, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> EmailExistsForOtherAsync(
        string email,
        int excludeUserId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Users WHERE Email = @Email AND Id != @Id AND TenantId = @TenantId AND IsDeleted = 0
                ) THEN 1 ELSE 0 END",
                new { Email = email, Id = excludeUserId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<UserPasswordInfo?> GetPasswordInfoAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var user = await connection.QuerySingleOrDefaultAsync<(int Id, int TenantId, string PasswordHash)>(
            new CommandDefinition(
                "SELECT Id, TenantId, PasswordHash FROM Users WHERE Id = @UserId AND IsDeleted = 0",
                new { UserId = userId },
                cancellationToken: cancellationToken));

        if (user == default)
            return null;

        return new UserPasswordInfo(user.Id, user.TenantId, user.PasswordHash);
    }

    public async Task EnsureOrgBelongsToTenantAsync(
        int tenantId,
        int? branchId,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (branchId.HasValue)
        {
            var ok = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Branches WHERE Id = @BranchId AND TenantId = @TenantId
                ) THEN 1 ELSE 0 END
                """, new { BranchId = branchId.Value, TenantId = tenantId }, cancellationToken: cancellationToken));
            if (!ok)
                throw new ConflictException("Branch does not belong to this company.");
        }

        if (departmentId.HasValue)
        {
            var ok = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Departments WHERE Id = @DepartmentId AND TenantId = @TenantId
                ) THEN 1 ELSE 0 END
                """, new { DepartmentId = departmentId.Value, TenantId = tenantId }, cancellationToken: cancellationToken));
            if (!ok)
                throw new ConflictException("Department does not belong to this company.");
        }
    }

    public async Task<int> InsertAsync(
        UserInsertModel model,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            return await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    @"INSERT INTO Users (
                        TenantId, FullName, Email, PasswordHash, Phone, Role, IsActive, CreatedAt, IsDeleted,
                        BranchId, DepartmentId, JobTitle, EmployeeCode, EmployeeType, Status,
                        DefaultWorkspaceKey, DefaultDashboardKey, HomeRoute, TimeZone, Language, Theme, AvatarUrl,
                        PasswordChangedAt)
                      VALUES (
                        @TenantId, @FullName, @Email, @PasswordHash, @Phone, @Role, @IsActive, @CreatedAt, 0,
                        @BranchId, @DepartmentId, @JobTitle, @EmployeeCode, @EmployeeType, @Status,
                        @DefaultWorkspaceKey, @DefaultDashboardKey, @HomeRoute, @TimeZone, @Language, @Theme, @AvatarUrl,
                        @PasswordChangedAt);
                      SELECT SCOPE_IDENTITY();",
                    new
                    {
                        model.TenantId,
                        model.FullName,
                        model.Email,
                        model.PasswordHash,
                        model.Phone,
                        Role = (int)model.Role,
                        model.IsActive,
                        model.CreatedAt,
                        model.PasswordChangedAt,
                        model.BranchId,
                        model.DepartmentId,
                        model.JobTitle,
                        model.EmployeeCode,
                        model.EmployeeType,
                        model.Status,
                        model.DefaultWorkspaceKey,
                        model.DefaultDashboardKey,
                        model.HomeRoute,
                        model.TimeZone,
                        model.Language,
                        model.Theme,
                        model.AvatarUrl
                    },
                    cancellationToken: cancellationToken));
        }
        catch (Exception ex) when (ex.Message.Contains("Invalid column", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase))
        {
            return await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    @"INSERT INTO Users (TenantId, FullName, Email, PasswordHash, Phone, Role, IsActive, CreatedAt, IsDeleted, BranchId, DepartmentId)
                      VALUES (@TenantId, @FullName, @Email, @PasswordHash, @Phone, @Role, @IsActive, @CreatedAt, 0, @BranchId, @DepartmentId);
                      SELECT SCOPE_IDENTITY();",
                    new
                    {
                        model.TenantId,
                        model.FullName,
                        model.Email,
                        model.PasswordHash,
                        model.Phone,
                        Role = (int)model.Role,
                        model.IsActive,
                        CreatedAt = DateTime.UtcNow,
                        model.BranchId,
                        model.DepartmentId
                    },
                    cancellationToken: cancellationToken));
        }
    }

    public async Task UpdateAsync(
        int id,
        UserUpdateModel model,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Users SET
                        TenantId = @TenantId,
                        FullName = @FullName, Email = @Email, Phone = @Phone,
                        Role = @Role, IsActive = @IsActive, UpdatedAt = @UpdatedAt,
                        BranchId = @BranchId, DepartmentId = @DepartmentId,
                        JobTitle = @JobTitle, EmployeeCode = @EmployeeCode, EmployeeType = @EmployeeType,
                        Status = @Status,
                        DefaultWorkspaceKey = @DefaultWorkspaceKey,
                        DefaultDashboardKey = @DefaultDashboardKey,
                        HomeRoute = @HomeRoute, TimeZone = @TimeZone,
                        Language = @Language, Theme = @Theme, AvatarUrl = @AvatarUrl
                      WHERE Id = @Id",
                    new
                    {
                        model.TenantId,
                        model.FullName,
                        model.Email,
                        model.Phone,
                        Role = (int)model.Role,
                        model.IsActive,
                        model.UpdatedAt,
                        Id = id,
                        model.BranchId,
                        model.DepartmentId,
                        model.JobTitle,
                        model.EmployeeCode,
                        model.EmployeeType,
                        model.Status,
                        model.DefaultWorkspaceKey,
                        model.DefaultDashboardKey,
                        model.HomeRoute,
                        model.TimeZone,
                        model.Language,
                        model.Theme,
                        model.AvatarUrl
                    },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Users SET TenantId = @TenantId, FullName = @FullName, Email = @Email, Phone = @Phone,
                      Role = @Role, IsActive = @IsActive, UpdatedAt = @UpdatedAt,
                      BranchId = @BranchId, DepartmentId = @DepartmentId
                      WHERE Id = @Id",
                    new
                    {
                        model.TenantId,
                        model.FullName,
                        model.Email,
                        model.Phone,
                        Role = (int)model.Role,
                        model.IsActive,
                        model.UpdatedAt,
                        Id = id,
                        model.BranchId,
                        model.DepartmentId
                    },
                    cancellationToken: cancellationToken));
        }
    }

    public async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { UpdatedAt = DateTime.UtcNow, Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(
        int id,
        bool isActive,
        string status,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE Users SET IsActive = @IsActive, Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                    new { IsActive = isActive, Status = status, UpdatedAt = DateTime.UtcNow, Id = id },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE Users SET IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                    new { IsActive = isActive, UpdatedAt = DateTime.UtcNow, Id = id },
                    cancellationToken: cancellationToken));
        }
    }

    public async Task UpdateProfileAsync(
        int userId,
        string fullName,
        string? phone,
        string? timeZone,
        string? language,
        string? theme,
        string? avatarUrl,
        string? jobTitle,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Users SET
                        FullName = @FullName,
                        Phone = @Phone,
                        TimeZone = COALESCE(@TimeZone, TimeZone),
                        Language = COALESCE(@Language, Language),
                        Theme = COALESCE(@Theme, Theme),
                        AvatarUrl = COALESCE(@AvatarUrl, AvatarUrl),
                        JobTitle = COALESCE(@JobTitle, JobTitle),
                        UpdatedAt = @UpdatedAt
                      WHERE Id = @UserId",
                    new
                    {
                        FullName = fullName,
                        Phone = phone,
                        TimeZone = timeZone,
                        Language = language,
                        Theme = theme,
                        AvatarUrl = avatarUrl,
                        JobTitle = jobTitle,
                        UpdatedAt = DateTime.UtcNow,
                        UserId = userId
                    },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE Users SET FullName = @FullName, Phone = @Phone, UpdatedAt = @UpdatedAt WHERE Id = @UserId",
                    new
                    {
                        FullName = fullName,
                        Phone = phone,
                        UpdatedAt = DateTime.UtcNow,
                        UserId = userId
                    },
                    cancellationToken: cancellationToken));
        }
    }

    public async Task ChangePasswordAsync(
        int userId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Users SET PasswordHash = @Hash, UpdatedAt = @UpdatedAt,
                  PasswordChangedAt = @PasswordChangedAt, FailedLoginAttempts = 0, LockoutEndUtc = NULL
                  WHERE Id = @Id",
                new
                {
                    Hash = passwordHash,
                    UpdatedAt = DateTime.UtcNow,
                    PasswordChangedAt = DateTime.UtcNow,
                    Id = userId
                },
                cancellationToken: cancellationToken));
    }

    public async Task AdminResetPasswordAsync(
        int userId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Users
                  SET PasswordHash = @PasswordHash,
                      RefreshToken = NULL,
                      RefreshTokenExpiryTime = NULL,
                      PasswordChangedAt = @PasswordChangedAt,
                      FailedLoginAttempts = 0,
                      LockoutEndUtc = NULL,
                      UpdatedAt = @UpdatedAt
                  WHERE Id = @Id",
                new
                {
                    PasswordHash = passwordHash,
                    PasswordChangedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Id = userId
                },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AssignedRoleDto>> GetAssignedRolesAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await LoadAssignedAsync(connection, userId, cancellationToken);
        return rows.Select(ToAssignedRoleDto).ToList();
    }

    public async Task<IReadOnlyList<string>> GetActiveRoleCodesAsync(
        int tenantId,
        IReadOnlyList<int> roleIds,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT Code FROM Roles
            WHERE TenantId = @TenantId AND Id IN @RoleIds AND IsActive = 1
            """,
            new { TenantId = tenantId, RoleIds = roleIds.Distinct().ToList() },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<bool> ActivePlatformRoleExistsAsync(
        int tenantId,
        string code,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Roles WHERE TenantId = @TenantId AND Code = @Code AND IsActive = 1) THEN 1 ELSE 0 END",
                new { TenantId = tenantId, Code = code },
                cancellationToken: cancellationToken));
    }

    public async Task SyncLegacyRoleAsync(
        int userId,
        int tenantId,
        UserRole legacyRole,
        int? branchId,
        int? departmentId,
        int? assignedBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var code = RoleRegistrySeed.MapLegacyRoleCode(legacyRole);
        var roleId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM Roles WHERE TenantId = @TenantId AND Code = @Code
            """, new { TenantId = tenantId, Code = code }, cancellationToken: cancellationToken));

        if (!roleId.HasValue)
            return;

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM UserRoles WHERE UserId = @UserId AND RoleId = @RoleId) THEN 1 ELSE 0 END
            """, new { UserId = userId, RoleId = roleId.Value }, cancellationToken: cancellationToken));

        if (exists)
            return;

        await InsertAssignmentAsync(
            connection, userId, roleId.Value, branchId, departmentId, assignedBy, cancellationToken);
    }

    public async Task AssignPlatformRoleAsync(
        int userId,
        int tenantId,
        string platformRoleCode,
        int? branchId,
        int? departmentId,
        int? assignedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(platformRoleCode))
            return;

        using var connection = dbFactory.CreateConnection();
        var code = platformRoleCode.Trim().ToUpperInvariant();
        var roleId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM Roles WHERE TenantId = @TenantId AND Code = @Code AND IsActive = 1
            """, new { TenantId = tenantId, Code = code }, cancellationToken: cancellationToken));

        if (!roleId.HasValue)
            throw new NotFoundException("Role", code);

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM UserRoles WHERE UserId = @UserId AND RoleId = @RoleId) THEN 1 ELSE 0 END
            """, new { UserId = userId, RoleId = roleId.Value }, cancellationToken: cancellationToken));

        if (exists)
            return;

        await InsertAssignmentAsync(
            connection, userId, roleId.Value, branchId, departmentId, assignedBy, cancellationToken);
    }

    public async Task ReplaceRoleAssignmentsAsync(
        int userId,
        int tenantId,
        IReadOnlyList<int> roleIds,
        IReadOnlyDictionary<int, (int? BranchId, int? DepartmentId)> scopes,
        int? assignedBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var distinct = roleIds.Distinct().ToList();
        if (distinct.Count > 0)
        {
            var validCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM Roles WHERE TenantId = @TenantId AND Id IN @RoleIds AND IsActive = 1
                """, new { TenantId = tenantId, RoleIds = distinct }, cancellationToken: cancellationToken));
            if (validCount != distinct.Count)
                throw new ConflictException(
                    "One or more roles are invalid, inactive, or do not belong to this company.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM UserRoles WHERE UserId = @UserId",
            new { UserId = userId },
            cancellationToken: cancellationToken));

        foreach (var roleId in distinct)
        {
            scopes.TryGetValue(roleId, out var scope);
            await InsertAssignmentAsync(
                connection, userId, roleId, scope.BranchId, scope.DepartmentId, assignedBy, cancellationToken);
        }
    }

    private static async Task<Dictionary<int, List<AssignedRoleDto>>> LoadAssignedRolesMapAsync(
        System.Data.IDbConnection connection,
        IReadOnlyList<int> userIds,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<int, List<AssignedRoleDto>>();
        if (userIds.Count == 0) return map;

        try
        {
            var rows = await connection.QueryAsync<(
                int UserId, int RoleId, string Code, string Name, string? DisplayName,
                string? Category, string? RoleType, int? BranchId, int? DepartmentId)>(
                new CommandDefinition("""
                    SELECT ur.UserId, r.Id AS RoleId, r.Code, r.Name,
                           COALESCE(r.DisplayName, r.Name) AS DisplayName,
                           r.Category,
                           COALESCE(r.RoleType, CASE WHEN r.IsSystem = 1 THEN N'System' ELSE N'Custom' END) AS RoleType,
                           ur.BranchId, ur.DepartmentId
                    FROM UserRoles ur
                    INNER JOIN Roles r ON r.Id = ur.RoleId
                    WHERE ur.UserId IN @UserIds
                    ORDER BY COALESCE(r.SortOrder, 0), r.Name
                    """,
                    new { UserIds = userIds },
                    cancellationToken: cancellationToken));

            foreach (var row in rows)
            {
                if (!map.TryGetValue(row.UserId, out var list))
                {
                    list = [];
                    map[row.UserId] = list;
                }

                list.Add(new AssignedRoleDto(
                    row.RoleId,
                    row.Code,
                    row.Name,
                    string.IsNullOrWhiteSpace(row.DisplayName) ? row.Name : row.DisplayName!,
                    row.Category,
                    row.RoleType,
                    row.BranchId,
                    row.DepartmentId));
            }
        }
        catch
        {
            // UserRoles / metadata may be unavailable.
        }

        return map;
    }

    private static async Task<IReadOnlyList<AssignedRoleRow>> LoadAssignedAsync(
        System.Data.IDbConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await connection.QueryAsync<AssignedRoleRow>(new CommandDefinition("""
                SELECT r.Id AS RoleId, r.Code, r.Name,
                       COALESCE(r.DisplayName, r.Name) AS DisplayName,
                       r.Category, COALESCE(r.RoleType, CASE WHEN r.IsSystem = 1 THEN N'System' ELSE N'Custom' END) AS RoleType,
                       ur.BranchId, ur.DepartmentId
                FROM UserRoles ur
                INNER JOIN Roles r ON r.Id = ur.RoleId
                WHERE ur.UserId = @UserId
                ORDER BY COALESCE(r.SortOrder, 0), r.Name
                """, new { UserId = userId }, cancellationToken: cancellationToken))).ToList();
        }
        catch
        {
            return (await connection.QueryAsync<AssignedRoleRow>(new CommandDefinition("""
                SELECT r.Id AS RoleId, r.Code, r.Name,
                       r.Name AS DisplayName,
                       CAST(NULL AS NVARCHAR(100)) AS Category,
                       CASE WHEN r.IsSystem = 1 THEN N'System' ELSE N'Custom' END AS RoleType,
                       CAST(NULL AS INT) AS BranchId,
                       CAST(NULL AS INT) AS DepartmentId
                FROM UserRoles ur
                INNER JOIN Roles r ON r.Id = ur.RoleId
                WHERE ur.UserId = @UserId
                ORDER BY r.Name
                """, new { UserId = userId }, cancellationToken: cancellationToken))).ToList();
        }
    }

    private static async Task InsertAssignmentAsync(
        System.Data.IDbConnection connection,
        int userId,
        int roleId,
        int? branchId,
        int? departmentId,
        int? assignedBy,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO UserRoles (UserId, RoleId, BranchId, DepartmentId, AssignedAt, AssignedBy)
                VALUES (@UserId, @RoleId, @BranchId, @DepartmentId, SYSUTCDATETIME(), @AssignedBy)
                """,
                new
                {
                    UserId = userId,
                    RoleId = roleId,
                    BranchId = branchId,
                    DepartmentId = departmentId,
                    AssignedBy = assignedBy
                },
                cancellationToken: cancellationToken));
        }
        catch
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)
                """, new { UserId = userId, RoleId = roleId }, cancellationToken: cancellationToken));
        }
    }

    private static UserDto ToDto(
        UserRow row,
        IReadOnlyList<AssignedRoleDto>? assignedRoles = null) => new(
        row.Id,
        row.FullName,
        row.Email,
        row.Phone ?? "",
        row.Role,
        row.IsActive,
        row.CreatedAt,
        row.CompanyId,
        row.CompanyName,
        row.BranchId,
        row.BranchName,
        row.DepartmentId,
        row.DepartmentName,
        row.JobTitle,
        row.EmployeeCode,
        row.EmployeeType,
        UserLifecycle.Normalize(row.Status, row.IsActive),
        row.DefaultWorkspaceKey,
        row.DefaultDashboardKey,
        row.HomeRoute,
        row.TimeZone,
        row.Language,
        row.Theme,
        row.AvatarUrl,
        assignedRoles,
        row.LastLoginAt);

    private static UserProfileDto ToProfileDto(
        UserRow row,
        IReadOnlyList<AssignedRoleDto>? assignedRoles = null,
        IReadOnlyList<Application.Common.Interfaces.EffectivePermissionDto>? effectivePermissions = null) => new(
        row.Id,
        row.FullName,
        row.Email,
        row.Phone,
        row.JobTitle,
        row.EmployeeCode,
        row.EmployeeType,
        UserLifecycle.Normalize(row.Status, row.IsActive),
        row.CompanyId,
        row.CompanyName,
        row.BranchId,
        row.BranchName,
        row.DepartmentId,
        row.DepartmentName,
        row.DefaultWorkspaceKey,
        row.DefaultDashboardKey,
        row.HomeRoute,
        row.TimeZone,
        row.Language,
        row.Theme,
        row.AvatarUrl,
        assignedRoles,
        effectivePermissions);

    private static AssignedRoleDto ToAssignedRoleDto(AssignedRoleRow row) => new(
        row.RoleId,
        row.Code,
        row.Name,
        string.IsNullOrWhiteSpace(row.DisplayName) ? row.Name : row.DisplayName!,
        row.Category,
        row.RoleType,
        row.BranchId,
        row.DepartmentId);

    private sealed class UserRow
    {
        public int Id { get; init; }
        public string FullName { get; init; } = "";
        public string Email { get; init; } = "";
        public string? Phone { get; init; }
        public UserRole Role { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public int? CompanyId { get; init; }
        public string? CompanyName { get; init; }
        public int? BranchId { get; init; }
        public string? BranchName { get; init; }
        public int? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public string? JobTitle { get; init; }
        public string? EmployeeCode { get; init; }
        public string? EmployeeType { get; init; }
        public string? Status { get; init; }
        public string? DefaultWorkspaceKey { get; init; }
        public string? DefaultDashboardKey { get; init; }
        public string? HomeRoute { get; init; }
        public string? TimeZone { get; init; }
        public string? Language { get; init; }
        public string? Theme { get; init; }
        public string? AvatarUrl { get; init; }
        public DateTime? LastLoginAt { get; init; }
    }

    private sealed class AssignedRoleRow
    {
        public int RoleId { get; init; }
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string? DisplayName { get; init; }
        public string? Category { get; init; }
        public string? RoleType { get; init; }
        public int? BranchId { get; init; }
        public int? DepartmentId { get; init; }
    }
}
