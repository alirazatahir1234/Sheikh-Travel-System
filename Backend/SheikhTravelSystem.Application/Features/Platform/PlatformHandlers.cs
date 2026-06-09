using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Platform;

internal static class BranchSql
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

public class GetBranchesQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetBranchesQuery, ApiResponse<IReadOnlyList<BranchDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<BranchDto>>> Handle(GetBranchesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await connection.QueryAsync<BranchDto>(new CommandDefinition(
            BranchSql.SelectColumns + " WHERE b.TenantId = @TenantId ORDER BY b.Name",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return ApiResponse<IReadOnlyList<BranchDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetBranchByIdQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetBranchByIdQuery, ApiResponse<BranchDto>>
{
    public async Task<ApiResponse<BranchDto>> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await connection.QuerySingleOrDefaultAsync<BranchDto>(new CommandDefinition(
            BranchSql.SelectColumns + " WHERE b.Id = @Id AND b.TenantId = @TenantId",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (row is null) throw new NotFoundException("Branch", request.Id);
        return ApiResponse<BranchDto>.SuccessResponse(row);
    }
}

public class CreateBranchCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<CreateBranchCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var payload = request.Payload;
        await BranchMutationHelper.ValidateAsync(connection, tenantId, null, payload, cancellationToken);

        var isActive = payload.Status == (int)BranchStatus.Active;
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Branches (
                TenantId, ParentBranchId, BranchCode, Name, BranchType, BranchManagerUserId,
                Phone, Email, Address, City, Country, TimeZone, CurrencyCode, Status, IsGpsEnabled, IsActive, CreatedAt)
            VALUES (
                @TenantId, @ParentBranchId, @BranchCode, @Name, @BranchType, @BranchManagerUserId,
                @Phone, @Email, @Address, @City, @Country, @TimeZone, @CurrencyCode, @Status, @IsGpsEnabled, @IsActive, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, BranchMutationHelper.ToParams(tenantId, null, payload, isActive),
            cancellationToken: cancellationToken));
        return ApiResponse<int>.SuccessResponse(id, "Branch created.");
    }
}

public class UpdateBranchCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateBranchCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var payload = request.Payload;
        await BranchMutationHelper.ValidateAsync(connection, tenantId, request.Id, payload, cancellationToken);

        var isActive = payload.Status == (int)BranchStatus.Active;
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Branches SET
                ParentBranchId = @ParentBranchId, BranchCode = @BranchCode, Name = @Name, BranchType = @BranchType,
                BranchManagerUserId = @BranchManagerUserId, Phone = @Phone, Email = @Email, Address = @Address,
                City = @City, Country = @Country, TimeZone = @TimeZone, CurrencyCode = @CurrencyCode,
                Status = @Status, IsGpsEnabled = @IsGpsEnabled, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, BranchMutationHelper.ToParams(tenantId, request.Id, payload, isActive),
            cancellationToken: cancellationToken));
        if (affected == 0) throw new NotFoundException("Branch", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Branch updated.");
    }
}

internal static class BranchMutationHelper
{
    internal static object ToParams(int tenantId, int? id, BranchUpsertPayload payload, bool isActive) => new
    {
        Id = id,
        TenantId = tenantId,
        ParentBranchId = payload.ParentBranchId,
        BranchCode = payload.BranchCode.Trim().ToUpperInvariant(),
        Name = payload.Name.Trim(),
        BranchType = payload.BranchType?.Trim(),
        BranchManagerUserId = payload.BranchManagerUserId,
        Phone = payload.Phone?.Trim(),
        Email = payload.Email?.Trim(),
        Address = payload.Address?.Trim(),
        City = payload.City?.Trim(),
        Country = payload.Country?.Trim(),
        TimeZone = payload.TimeZone?.Trim(),
        CurrencyCode = payload.CurrencyCode?.Trim()?.ToUpperInvariant(),
        payload.Status,
        payload.IsGpsEnabled,
        IsActive = isActive
    };

    internal static async Task ValidateAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        int? branchId,
        BranchUpsertPayload payload,
        CancellationToken ct)
    {
        var code = payload.BranchCode.Trim().ToUpperInvariant();
        var codeExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM Branches
            WHERE TenantId = @TenantId AND BranchCode = @BranchCode AND (@BranchId IS NULL OR Id <> @BranchId)
            """, new { TenantId = tenantId, BranchCode = code, BranchId = branchId }, cancellationToken: ct));
        if (codeExists > 0)
            throw new ConflictException($"Branch code '{code}' already exists for this tenant.");

        if (payload.ParentBranchId.HasValue)
        {
            if (branchId.HasValue && payload.ParentBranchId.Value == branchId.Value)
                throw new ConflictException("A branch cannot be its own parent.");

            var parentExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Branches WHERE Id = @Id AND TenantId = @TenantId",
                new { Id = payload.ParentBranchId.Value, TenantId = tenantId }, cancellationToken: ct));
            if (parentExists == 0)
                throw new NotFoundException("Parent branch", payload.ParentBranchId.Value);
        }

        if (payload.BranchManagerUserId.HasValue)
        {
            var managerExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Users WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = payload.BranchManagerUserId.Value, TenantId = tenantId }, cancellationToken: ct));
            if (managerExists == 0)
                throw new NotFoundException("Branch manager user", payload.BranchManagerUserId.Value);
        }
    }
}

public class DeleteBranchCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<DeleteBranchCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Branches WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (affected == 0) throw new NotFoundException("Branch", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Branch deleted.");
    }
}

public class GetDepartmentsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDepartmentsQuery, ApiResponse<IReadOnlyList<DepartmentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DepartmentDto>>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await connection.QueryAsync<DepartmentDto>(new CommandDefinition("""
            SELECT d.Id, d.TenantId, d.BranchId, d.Name, d.DepartmentHeadUserId,
                   h.FullName AS DepartmentHeadName, d.IsActive, d.CreatedAt,
                   (SELECT COUNT(*) FROM Users u
                    WHERE u.DepartmentId = d.Id AND u.TenantId = d.TenantId AND u.IsDeleted = 0) AS StaffCount
            FROM Departments d
            LEFT JOIN Users h ON h.Id = d.DepartmentHeadUserId AND h.IsDeleted = 0
            WHERE d.TenantId = @TenantId
            ORDER BY d.Name
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return ApiResponse<IReadOnlyList<DepartmentDto>>.SuccessResponse(rows.ToList());
    }
}

public class CreateDepartmentCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<CreateDepartmentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var payload = request.Payload;
        await DepartmentMutationHelper.ValidateHeadAsync(connection, tenantId, payload.DepartmentHeadUserId, cancellationToken);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Departments (TenantId, Name, DepartmentHeadUserId, IsActive, CreatedAt)
            VALUES (@TenantId, @Name, @DepartmentHeadUserId, 1, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
            {
                TenantId = tenantId,
                Name = payload.Name.Trim(),
                payload.DepartmentHeadUserId
            }, cancellationToken: cancellationToken));
        return ApiResponse<int>.SuccessResponse(id, "Department created.");
    }
}

public class UpdateDepartmentCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateDepartmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var payload = request.Payload;
        await DepartmentMutationHelper.ValidateHeadAsync(connection, tenantId, payload.DepartmentHeadUserId, cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Departments SET Name = @Name, DepartmentHeadUserId = @DepartmentHeadUserId,
                IsActive = @IsActive, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
            {
                request.Id,
                TenantId = tenantId,
                Name = payload.Name.Trim(),
                payload.DepartmentHeadUserId,
                request.IsActive
            }, cancellationToken: cancellationToken));
        if (affected == 0) throw new NotFoundException("Department", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Department updated.");
    }
}

internal static class DepartmentMutationHelper
{
    internal static async Task ValidateHeadAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        int? headUserId,
        CancellationToken ct)
    {
        if (!headUserId.HasValue) return;

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Users WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = headUserId.Value, TenantId = tenantId }, cancellationToken: ct));
        if (exists == 0)
            throw new NotFoundException("Department head user", headUserId.Value);
    }
}

public class DeleteDepartmentCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<DeleteDepartmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Departments WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (affected == 0) throw new NotFoundException("Department", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Department deleted.");
    }
}

public class GetPermissionsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPermissionsQuery, ApiResponse<IReadOnlyList<PermissionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PermissionDto>>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PermissionDto>(new CommandDefinition(
            "SELECT Id, ModuleName, PermissionCode, Description FROM Permissions ORDER BY ModuleName, PermissionCode",
            cancellationToken: cancellationToken));
        return ApiResponse<IReadOnlyList<PermissionDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetTenantModulesQueryHandler
    : IRequestHandler<GetTenantModulesQuery, ApiResponse<IReadOnlyList<TenantModuleDefinitionDto>>>
{
    public Task<ApiResponse<IReadOnlyList<TenantModuleDefinitionDto>>> Handle(
        GetTenantModulesQuery request, CancellationToken cancellationToken)
    {
        var modules = TenantModuleCatalog.All
            .Select(m => new TenantModuleDefinitionDto(m.Code, m.Name, m.LegacyKeys))
            .ToList();
        return Task.FromResult(ApiResponse<IReadOnlyList<TenantModuleDefinitionDto>>.SuccessResponse(modules));
    }
}

public class GetRolesQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetRolesQuery, ApiResponse<IReadOnlyList<RoleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<RoleDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var roles = (await connection.QueryAsync<(int Id, int TenantId, string Name, string Code, bool IsSystem, bool IsActive)>(
            new CommandDefinition(
                "SELECT Id, TenantId, Name, Code, IsSystem, IsActive FROM Roles WHERE TenantId = @TenantId ORDER BY Name",
                new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        var roleIds = roles.Select(r => r.Id).ToArray();
        var permMap = new Dictionary<int, List<string>>();
        if (roleIds.Length > 0)
        {
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
        }

        var dtos = roles.Select(r => new RoleDto(
            r.Id, r.TenantId, r.Name, r.Code, r.IsSystem, r.IsActive,
            permMap.TryGetValue(r.Id, out var perms) ? perms : Array.Empty<string>())).ToList();

        return ApiResponse<IReadOnlyList<RoleDto>>.SuccessResponse(dtos);
    }
}

public class CreateRoleCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<CreateRoleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var code = request.Code.Trim().ToUpperInvariant();

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Roles WHERE TenantId = @TenantId AND Code = @Code) THEN 1 ELSE 0 END",
            new { TenantId = tenantId, Code = code }, cancellationToken: cancellationToken));
        if (exists) throw new ConflictException($"Role code '{code}' already exists.");

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
            VALUES (@TenantId, @Name, @Code, 0, 1, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new { TenantId = tenantId, Name = request.Name.Trim(), Code = code }, cancellationToken: cancellationToken));
        return ApiResponse<int>.SuccessResponse(id, "Role created.");
    }
}

public class UpdateRolePermissionsCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateRolePermissionsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var roleExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Roles WHERE Id = @RoleId AND TenantId = @TenantId) THEN 1 ELSE 0 END",
            new { request.RoleId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (!roleExists) throw new NotFoundException("Role", request.RoleId);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM RolePermissions WHERE RoleId = @RoleId",
            new { request.RoleId }, cancellationToken: cancellationToken));

        foreach (var code in (request.PermissionCodes ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT @RoleId, p.Id FROM Permissions p WHERE p.PermissionCode = @Code
                """, new { request.RoleId, Code = code }, cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, "Role permissions updated.");
    }
}

public class GetTenantsQueryHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope) : IRequestHandler<GetTenantsQuery, ApiResponse<IReadOnlyList<TenantListDto>>>
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

    public async Task<ApiResponse<IReadOnlyList<TenantListDto>>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        IEnumerable<TenantListDto> rows;
        if (platformScope.IsSuperAdmin)
        {
            rows = await connection.QueryAsync<TenantListDto>(new CommandDefinition(
                TenantSelectSql + " ORDER BY t.Name",
                cancellationToken: cancellationToken));
        }
        else
        {
            rows = await connection.QueryAsync<TenantListDto>(new CommandDefinition(
                TenantSelectSql + " WHERE t.Id = @TenantId ORDER BY t.Name",
                new { TenantId = platformScope.TenantId }, cancellationToken: cancellationToken));
        }

        return ApiResponse<IReadOnlyList<TenantListDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetTenantByIdQueryHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope) : IRequestHandler<GetTenantByIdQuery, ApiResponse<TenantDetailDto>>
{
    public async Task<ApiResponse<TenantDetailDto>> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.Id);

        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<TenantDetailDto>(new CommandDefinition("""
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
            """, new { request.Id }, cancellationToken: cancellationToken));

        if (row is null) throw new NotFoundException("Tenant", request.Id);

        var moduleCodes = (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT m.ModuleCode
            FROM TenantModules tm
            INNER JOIN Modules m ON m.Id = tm.ModuleId
            WHERE tm.TenantId = @Id
            ORDER BY m.ModuleCode
            """, new { request.Id }, cancellationToken: cancellationToken))).ToList();

        if (moduleCodes.Count == 0)
            moduleCodes = TenantModuleCatalog.CodesFromLegacyKeys(
                await GetLegacyKeysAsync(connection, request.Id, cancellationToken)).ToList();

        var adminInfo = await connection.QuerySingleOrDefaultAsync<TenantAdminInfoDto>(new CommandDefinition("""
            SELECT TOP 1 Id, FullName, Email, Phone,
                   IsActive,
                   CASE WHEN IsActive = 1 THEN N'Active' ELSE N'Disabled' END AS Status
            FROM Users
            WHERE TenantId = @Id AND Role = 1 AND IsDeleted = 0
            ORDER BY Id
            """, new { request.Id }, cancellationToken: cancellationToken));

        var detail = row with { ModuleCodes = moduleCodes, AdminInfo = adminInfo };
        return ApiResponse<TenantDetailDto>.SuccessResponse(detail);
    }

    private static async Task<IReadOnlyList<string>> GetLegacyKeysAsync(
        System.Data.IDbConnection connection, int tenantId, CancellationToken ct)
    {
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT EnabledModulesJson FROM Tenants WHERE Id = @Id",
            new { Id = tenantId }, cancellationToken: ct));
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }
}

public class ResetTenantAdminPasswordCommandHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope,
    IPasswordHasher passwordHasher) : IRequestHandler<ResetTenantAdminPasswordCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ResetTenantAdminPasswordCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return ApiResponse<bool>.FailResponse("Password must be at least 8 characters.");

        var hash = passwordHasher.Hash(request.NewPassword);
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Users SET PasswordHash = @Hash
            WHERE TenantId = @TenantId AND Role = 1 AND IsDeleted = 0
            """, new { Hash = hash, request.TenantId }, cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true)
            : ApiResponse<bool>.FailResponse("Admin user not found for this tenant.");
    }
}

public class GetTenantManagementStatsQueryHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope) : IRequestHandler<GetTenantManagementStatsQuery, ApiResponse<TenantManagementStatsDto>>
{
    public async Task<ApiResponse<TenantManagementStatsDto>> Handle(GetTenantManagementStatsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        if (platformScope.IsSuperAdmin)
        {
            var stats = await connection.QuerySingleAsync<TenantManagementStatsDto>(new CommandDefinition("""
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
            return ApiResponse<TenantManagementStatsDto>.SuccessResponse(stats);
        }

        var tenantId = platformScope.TenantId;
        var scoped = await connection.QuerySingleAsync<TenantManagementStatsDto>(new CommandDefinition("""
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
        return ApiResponse<TenantManagementStatsDto>.SuccessResponse(scoped);
    }
}

public class UpdateTenantCommandHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope,
    ITenantModuleService tenantModuleService)
    : IRequestHandler<UpdateTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.Id);

        using var connection = dbFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Tenants
            SET Name = @Name, SubscriptionPlan = @SubscriptionPlan, IsActive = @IsActive,
                Status = CASE WHEN @IsActive = 1 THEN N'Active' ELSE N'Suspended' END,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id
            """, new
        {
            request.Id,
            Name = request.Name.Trim(),
            request.SubscriptionPlan,
            request.IsActive
        }, cancellationToken: cancellationToken));

        if (affected == 0) throw new NotFoundException("Tenant", request.Id);

        if (!string.IsNullOrWhiteSpace(request.SubscriptionPlan)
            || request.MaxUsers.HasValue
            || request.MaxVehicles.HasValue
            || request.MaxDrivers.HasValue
            || request.MaxBranches.HasValue
            || request.MaxGpsDevices.HasValue)
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
                request.Id,
                PlanName = request.SubscriptionPlan,
                request.MaxUsers,
                request.MaxVehicles,
                request.MaxDrivers,
                request.MaxBranches,
                request.MaxGpsDevices
            }, cancellationToken: cancellationToken));
        }

        IReadOnlyList<string>? moduleCodes = null;
        if (request.ModuleCodes is { Count: > 0 })
            moduleCodes = request.ModuleCodes;
        else if (request.EnabledModules is { Count: > 0 })
            moduleCodes = TenantModuleCatalog.CodesFromLegacyKeys(request.EnabledModules);

        if (moduleCodes is { Count: > 0 })
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM TenantModules WHERE TenantId = @Id",
                new { request.Id }, cancellationToken: cancellationToken));

            foreach (var code in moduleCodes)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO TenantModules (TenantId, ModuleId)
                    SELECT @Id, m.Id FROM Modules m WHERE m.ModuleCode = @Code;
                    """, new { request.Id, Code = code }, cancellationToken: cancellationToken));
            }

            await tenantModuleService.SyncLegacyJsonAsync(request.Id, moduleCodes, cancellationToken);
        }

        return ApiResponse<bool>.SuccessResponse(true, "Tenant updated.");
    }
}

public class GetUserMenuQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IUserAccessService userAccessService,
    ITenantModuleService tenantModuleService)
    : IRequestHandler<GetUserMenuQuery, ApiResponse<IReadOnlyList<MenuModuleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<MenuModuleDto>>> Handle(GetUserMenuQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");
        var tenantId = tenantContext.GetRequiredTenantId();

        using var connection = dbFactory.CreateConnection();
        var access = await userAccessService.ResolveAsync(userId, tenantId, cancellationToken);
        var permissionSet = access.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var enabledModules = await tenantModuleService.GetLegacyModuleKeysAsync(tenantId, cancellationToken);

        var modules = (await connection.QueryAsync<(int Id, string Name, string ModuleKey, string? Icon, int SortOrder, bool IsCollapsible)>(
            new CommandDefinition(
                "SELECT Id, Name, ModuleKey, Icon, SortOrder, IsCollapsible FROM PlatformModules ORDER BY SortOrder",
                cancellationToken: cancellationToken))).ToList();

        var menus = (await connection.QueryAsync<(int Id, int ModuleId, string Name, string? Route, string? Icon, string? PermissionCode, int SortOrder)>(
            new CommandDefinition(
                "SELECT Id, ModuleId, Name, Route, Icon, PermissionCode, SortOrder FROM PlatformMenus WHERE IsActive = 1 ORDER BY SortOrder",
                cancellationToken: cancellationToken))).ToList();

        var result = new List<MenuModuleDto>();
        foreach (var module in modules)
        {
            if (enabledModules.Count > 0 && !IsModuleEnabled(module.ModuleKey, enabledModules))
                continue;

            var items = menus
                .Where(m => m.ModuleId == module.Id)
                .Where(m => string.IsNullOrEmpty(m.PermissionCode) || permissionSet.Contains(m.PermissionCode))
                .Select(m => new MenuItemDto(
                    Slugify(m.Name),
                    m.Name,
                    m.Icon ?? "circle",
                    m.Route ?? "/dashboard",
                    m.PermissionCode,
                    m.SortOrder))
                .ToList();

            if (items.Count == 0) continue;

            result.Add(new MenuModuleDto(
                module.ModuleKey,
                module.Name,
                module.Icon ?? "folder",
                module.IsCollapsible,
                module.SortOrder,
                items));
        }

        return ApiResponse<IReadOnlyList<MenuModuleDto>>.SuccessResponse(result);
    }

    private static bool IsModuleEnabled(string moduleKey, IReadOnlyList<string> enabled) =>
        moduleKey switch
        {
            "dashboard" => enabled.Contains("dashboard", StringComparer.OrdinalIgnoreCase),
            "operations" => enabled.Any(k => k is "bookings" or "routes"),
            "fleet" => enabled.Any(k => k is "vehicles" or "drivers" or "gps-tracking" or "fuel-logs" or "maintenance"),
            "customers" => enabled.Contains("customers", StringComparer.OrdinalIgnoreCase),
            "finance" => enabled.Contains("payments", StringComparer.OrdinalIgnoreCase),
            "analytics" => enabled.Any(k => k is "reports" or "audit-logs"),
            "administration" => enabled.Any(k => k is "users" or "driver-allowance-rules"),
            "organization" => enabled.Any(k => k is "users" or "driver-allowance-rules"),
            "access_control" => enabled.Any(k => k is "users" or "driver-allowance-rules"),
            _ => true
        };

    private static string Slugify(string value) =>
        value.Trim().ToLowerInvariant().Replace(' ', '-').Replace('&', '-');
}

// Organization Designer Handlers

public class GetOrganizationTreeQueryHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope) : IRequestHandler<GetOrganizationTreeQuery, ApiResponse<OrganizationTreeDto>>
{
    public async Task<ApiResponse<OrganizationTreeDto>> Handle(GetOrganizationTreeQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        using var connection = dbFactory.CreateConnection();

        var tenantName = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT Name FROM Tenants WHERE Id = @TenantId",
            new { request.TenantId }, cancellationToken: cancellationToken))
            ?? throw new NotFoundException("Tenant", request.TenantId);

        var branches = (await connection.QueryAsync<(int Id, int? ParentBranchId, string BranchCode, string Name, string? BranchType, string? City, string? Country, bool IsActive, int Status)>(
            new CommandDefinition("""
                SELECT Id, ParentBranchId, BranchCode, Name, BranchType, City, Country, IsActive, Status
                FROM Branches WHERE TenantId = @TenantId ORDER BY Name
                """, new { request.TenantId }, cancellationToken: cancellationToken))).ToList();

        var departments = (await connection.QueryAsync<(int Id, int? BranchId, string Name, string? DepartmentHeadName, int StaffCount, bool IsActive)>(
            new CommandDefinition("""
                SELECT d.Id, d.BranchId, d.Name,
                       h.FullName AS DepartmentHeadName,
                       (SELECT COUNT(*) FROM Users u WHERE u.DepartmentId = d.Id AND u.TenantId = d.TenantId AND u.IsDeleted = 0) AS StaffCount,
                       d.IsActive
                FROM Departments d
                LEFT JOIN Users h ON h.Id = d.DepartmentHeadUserId AND h.IsDeleted = 0
                WHERE d.TenantId = @TenantId
                ORDER BY d.Name
                """, new { request.TenantId }, cancellationToken: cancellationToken))).ToList();

        var deptLookup = departments.ToLookup(d => d.BranchId);

        var branchDtos = branches.Select(b => new OrganizationBranchDto(
            b.Id, b.ParentBranchId, b.BranchCode, b.Name, b.BranchType, b.City, b.Country, b.IsActive, b.Status,
            deptLookup[b.Id].Select(d => new OrganizationDepartmentDto(d.Id, d.BranchId, d.Name, d.DepartmentHeadName, d.StaffCount, d.IsActive)).ToList()
        )).ToList();

        var unassignedDepts = deptLookup[null]
            .Select(d => new OrganizationDepartmentDto(d.Id, d.BranchId, d.Name, d.DepartmentHeadName, d.StaffCount, d.IsActive))
            .ToList();

        return ApiResponse<OrganizationTreeDto>.SuccessResponse(
            new OrganizationTreeDto(request.TenantId, tenantName, branchDtos, unassignedDepts));
    }
}

// Tenant-scoped Branch Handlers

public class GetBranchesForTenantQueryHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<GetBranchesForTenantQuery, ApiResponse<IReadOnlyList<BranchDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<BranchDto>>> Handle(GetBranchesForTenantQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<BranchDto>(new CommandDefinition(
            BranchSql.SelectColumns + " WHERE b.TenantId = @TenantId ORDER BY b.Name",
            new { request.TenantId }, cancellationToken: cancellationToken));
        return ApiResponse<IReadOnlyList<BranchDto>>.SuccessResponse(rows.ToList());
    }
}

public class CreateBranchForTenantCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<CreateBranchForTenantCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateBranchForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        using var connection = dbFactory.CreateConnection();
        var payload = request.Payload;
        await BranchMutationHelper.ValidateAsync(connection, request.TenantId, null, payload, cancellationToken);

        var isActive = payload.Status == (int)BranchStatus.Active;
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Branches (
                TenantId, ParentBranchId, BranchCode, Name, BranchType, BranchManagerUserId,
                Phone, Email, Address, City, Country, TimeZone, CurrencyCode, Status, IsGpsEnabled, IsActive, CreatedAt)
            VALUES (
                @TenantId, @ParentBranchId, @BranchCode, @Name, @BranchType, @BranchManagerUserId,
                @Phone, @Email, @Address, @City, @Country, @TimeZone, @CurrencyCode, @Status, @IsGpsEnabled, @IsActive, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, BranchMutationHelper.ToParams(request.TenantId, null, payload, isActive),
            cancellationToken: cancellationToken));
        return ApiResponse<int>.SuccessResponse(id, "Branch created.");
    }
}

public class UpdateBranchForTenantCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<UpdateBranchForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateBranchForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        using var connection = dbFactory.CreateConnection();
        var payload = request.Payload;
        await BranchMutationHelper.ValidateAsync(connection, request.TenantId, request.BranchId, payload, cancellationToken);

        var isActive = payload.Status == (int)BranchStatus.Active;
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Branches SET
                ParentBranchId = @ParentBranchId, BranchCode = @BranchCode, Name = @Name, BranchType = @BranchType,
                BranchManagerUserId = @BranchManagerUserId, Phone = @Phone, Email = @Email, Address = @Address,
                City = @City, Country = @Country, TimeZone = @TimeZone, CurrencyCode = @CurrencyCode,
                Status = @Status, IsGpsEnabled = @IsGpsEnabled, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, BranchMutationHelper.ToParams(request.TenantId, request.BranchId, payload, isActive),
            cancellationToken: cancellationToken));
        if (affected == 0) throw new NotFoundException("Branch", request.BranchId);
        return ApiResponse<bool>.SuccessResponse(true, "Branch updated.");
    }
}

public class DeleteBranchForTenantCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<DeleteBranchForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteBranchForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Departments SET BranchId = NULL WHERE BranchId = @BranchId AND TenantId = @TenantId",
            new { request.BranchId, request.TenantId }, cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Branches WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = request.BranchId, request.TenantId }, cancellationToken: cancellationToken));
        if (affected == 0) throw new NotFoundException("Branch", request.BranchId);
        return ApiResponse<bool>.SuccessResponse(true, "Branch deleted.");
    }
}

// Tenant-scoped Department Handlers

public class GetDepartmentsForTenantQueryHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<GetDepartmentsForTenantQuery, ApiResponse<IReadOnlyList<DepartmentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DepartmentDto>>> Handle(GetDepartmentsForTenantQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<DepartmentDto>(new CommandDefinition("""
            SELECT d.Id, d.TenantId, d.BranchId, d.Name, d.DepartmentHeadUserId,
                   h.FullName AS DepartmentHeadName, d.IsActive, d.CreatedAt,
                   (SELECT COUNT(*) FROM Users u
                    WHERE u.DepartmentId = d.Id AND u.TenantId = d.TenantId AND u.IsDeleted = 0) AS StaffCount
            FROM Departments d
            LEFT JOIN Users h ON h.Id = d.DepartmentHeadUserId AND h.IsDeleted = 0
            WHERE d.TenantId = @TenantId
            ORDER BY d.Name
            """, new { request.TenantId }, cancellationToken: cancellationToken));
        return ApiResponse<IReadOnlyList<DepartmentDto>>.SuccessResponse(rows.ToList());
    }
}

public class CreateDepartmentForTenantCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<CreateDepartmentForTenantCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateDepartmentForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        using var connection = dbFactory.CreateConnection();
        var payload = request.Payload;
        await DepartmentMutationHelper.ValidateHeadAsync(connection, request.TenantId, payload.DepartmentHeadUserId, cancellationToken);

        if (payload.BranchId.HasValue)
        {
            var branchExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Branches WHERE Id = @Id AND TenantId = @TenantId",
                new { Id = payload.BranchId.Value, request.TenantId }, cancellationToken: cancellationToken));
            if (branchExists == 0) throw new NotFoundException("Branch", payload.BranchId.Value);
        }

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Departments (TenantId, BranchId, Name, DepartmentHeadUserId, IsActive, CreatedAt)
            VALUES (@TenantId, @BranchId, @Name, @DepartmentHeadUserId, 1, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
            {
                request.TenantId,
                payload.BranchId,
                Name = payload.Name.Trim(),
                payload.DepartmentHeadUserId
            }, cancellationToken: cancellationToken));
        return ApiResponse<int>.SuccessResponse(id, "Department created.");
    }
}

public class UpdateDepartmentForTenantCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<UpdateDepartmentForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateDepartmentForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        using var connection = dbFactory.CreateConnection();
        var payload = request.Payload;
        await DepartmentMutationHelper.ValidateHeadAsync(connection, request.TenantId, payload.DepartmentHeadUserId, cancellationToken);

        if (payload.BranchId.HasValue)
        {
            var branchExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Branches WHERE Id = @Id AND TenantId = @TenantId",
                new { Id = payload.BranchId.Value, request.TenantId }, cancellationToken: cancellationToken));
            if (branchExists == 0) throw new NotFoundException("Branch", payload.BranchId.Value);
        }

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Departments SET Name = @Name, BranchId = @BranchId, DepartmentHeadUserId = @DepartmentHeadUserId,
                IsActive = @IsActive, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
            {
                Id = request.DepartmentId,
                request.TenantId,
                Name = payload.Name.Trim(),
                payload.BranchId,
                payload.DepartmentHeadUserId,
                request.IsActive
            }, cancellationToken: cancellationToken));
        if (affected == 0) throw new NotFoundException("Department", request.DepartmentId);
        return ApiResponse<bool>.SuccessResponse(true, "Department updated.");
    }
}

public class DeleteDepartmentForTenantCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<DeleteDepartmentForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteDepartmentForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        using var connection = dbFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Departments WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = request.DepartmentId, request.TenantId }, cancellationToken: cancellationToken));
        if (affected == 0) throw new NotFoundException("Department", request.DepartmentId);
        return ApiResponse<bool>.SuccessResponse(true, "Department deleted.");
    }
}

public class MoveDepartmentCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<MoveDepartmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(MoveDepartmentCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        using var connection = dbFactory.CreateConnection();

        if (request.NewBranchId.HasValue)
        {
            var branchExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM Branches WHERE Id = @Id AND TenantId = @TenantId",
                new { Id = request.NewBranchId.Value, request.TenantId }, cancellationToken: cancellationToken));
            if (branchExists == 0) throw new NotFoundException("Branch", request.NewBranchId.Value);
        }

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Departments SET BranchId = @NewBranchId, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = request.DepartmentId, request.TenantId, request.NewBranchId },
            cancellationToken: cancellationToken));
        if (affected == 0) throw new NotFoundException("Department", request.DepartmentId);
        return ApiResponse<bool>.SuccessResponse(true, "Department moved.");
    }
}

// Access Control (Sprint 2)

internal static class TenantRoleQueryHelper
{
    internal static async Task<IReadOnlyList<RoleSummaryDto>> LoadRoleSummariesAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        CancellationToken ct)
    {
        var roles = (await connection.QueryAsync<(int Id, int TenantId, string Name, string Code, bool IsSystem, bool IsActive)>(
            new CommandDefinition(
                "SELECT Id, TenantId, Name, Code, IsSystem, IsActive FROM Roles WHERE TenantId = @TenantId ORDER BY Name",
                new { TenantId = tenantId }, cancellationToken: ct))).ToList();

        if (roles.Count == 0) return Array.Empty<RoleSummaryDto>();

        var roleIds = roles.Select(r => r.Id).ToArray();
        var userCounts = (await connection.QueryAsync<(int RoleId, int UserCount)>(new CommandDefinition("""
            SELECT ur.RoleId, COUNT(*) AS UserCount
            FROM UserRoles ur
            INNER JOIN Users u ON u.Id = ur.UserId AND u.IsDeleted = 0
            WHERE ur.RoleId IN @RoleIds
            GROUP BY ur.RoleId
            """, new { RoleIds = roleIds }, cancellationToken: ct)))
            .ToDictionary(x => x.RoleId, x => x.UserCount);

        var permMap = new Dictionary<int, List<string>>();
        var pairs = await connection.QueryAsync<(int RoleId, string PermissionCode)>(new CommandDefinition(
            @"SELECT rp.RoleId, p.PermissionCode
              FROM RolePermissions rp
              INNER JOIN Permissions p ON p.Id = rp.PermissionId
              WHERE rp.RoleId IN @RoleIds",
            new { RoleIds = roleIds }, cancellationToken: ct));

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
            var perms = permMap.TryGetValue(r.Id, out var list) ? list : [];
            return new RoleSummaryDto(
                r.Id, r.TenantId, r.Name, r.Code, r.IsSystem, r.IsActive,
                userCounts.GetValueOrDefault(r.Id),
                perms.Count,
                perms);
        }).ToList();
    }
}

public class GetRolesForTenantQueryHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<GetRolesForTenantQuery, ApiResponse<IReadOnlyList<RoleSummaryDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<RoleSummaryDto>>> Handle(
        GetRolesForTenantQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        using var connection = dbFactory.CreateConnection();
        var rows = await TenantRoleQueryHelper.LoadRoleSummariesAsync(connection, request.TenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<RoleSummaryDto>>.SuccessResponse(rows);
    }
}

public class CreateRoleForTenantCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<CreateRoleForTenantCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateRoleForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        using var connection = dbFactory.CreateConnection();
        var code = request.Code.Trim().ToUpperInvariant();

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Roles WHERE TenantId = @TenantId AND Code = @Code) THEN 1 ELSE 0 END",
            new { request.TenantId, Code = code }, cancellationToken: cancellationToken));
        if (exists) throw new ConflictException($"Role code '{code}' already exists.");

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
            VALUES (@TenantId, @Name, @Code, 0, 1, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new { request.TenantId, Name = request.Name.Trim(), Code = code },
            cancellationToken: cancellationToken));
        return ApiResponse<int>.SuccessResponse(id, "Role created.");
    }
}

public class UpdateRoleForTenantCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<UpdateRoleForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateRoleForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        using var connection = dbFactory.CreateConnection();

        var role = await connection.QuerySingleOrDefaultAsync<(bool IsSystem, string Code)>(new CommandDefinition(
            "SELECT IsSystem, Code FROM Roles WHERE Id = @Id AND TenantId = @TenantId",
            new { request.RoleId, request.TenantId }, cancellationToken: cancellationToken));
        if (role.Code is null) throw new NotFoundException("Role", request.RoleId);

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Roles SET Name = @Name, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            Id = request.RoleId,
            request.TenantId,
            Name = request.Name.Trim(),
            request.IsActive
        }, cancellationToken: cancellationToken));

        if (affected == 0) throw new NotFoundException("Role", request.RoleId);
        return ApiResponse<bool>.SuccessResponse(true, "Role updated.");
    }
}

public class DeleteRoleForTenantCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<DeleteRoleForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteRoleForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        using var connection = dbFactory.CreateConnection();

        var role = await connection.QuerySingleOrDefaultAsync<(bool IsSystem, string? Code)>(new CommandDefinition(
            "SELECT IsSystem, Code FROM Roles WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = request.RoleId, request.TenantId }, cancellationToken: cancellationToken));
        if (role.Code is null) throw new NotFoundException("Role", request.RoleId);
        if (role.IsSystem) throw new ConflictException("System roles cannot be deleted.");

        var userCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM UserRoles ur
            INNER JOIN Users u ON u.Id = ur.UserId AND u.IsDeleted = 0
            WHERE ur.RoleId = @RoleId
            """, new { request.RoleId }, cancellationToken: cancellationToken));
        if (userCount > 0) throw new ConflictException("Cannot delete a role that is assigned to users.");

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Roles WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = request.RoleId, request.TenantId }, cancellationToken: cancellationToken));
        if (affected == 0) throw new NotFoundException("Role", request.RoleId);
        return ApiResponse<bool>.SuccessResponse(true, "Role deleted.");
    }
}

public class UpdateRolePermissionsForTenantCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<UpdateRolePermissionsForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateRolePermissionsForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        using var connection = dbFactory.CreateConnection();

        var roleExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Roles WHERE Id = @RoleId AND TenantId = @TenantId) THEN 1 ELSE 0 END",
            new { request.RoleId, request.TenantId }, cancellationToken: cancellationToken));
        if (!roleExists) throw new NotFoundException("Role", request.RoleId);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM RolePermissions WHERE RoleId = @RoleId",
            new { request.RoleId }, cancellationToken: cancellationToken));

        foreach (var code in (request.PermissionCodes ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT @RoleId, p.Id FROM Permissions p WHERE p.PermissionCode = @Code
                """, new { request.RoleId, Code = code }, cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, "Role permissions updated.");
    }
}

public class GetTenantSecuritySettingsQueryHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<GetTenantSecuritySettingsQuery, ApiResponse<TenantSecuritySettingsDto>>
{
    public async Task<ApiResponse<TenantSecuritySettingsDto>> Handle(
        GetTenantSecuritySettingsQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<TenantSecuritySettingsDto>(new CommandDefinition("""
            SELECT IsMfaRequired, PasswordExpiryDays, SessionTimeoutMinutes,
                   IsGdprEnabled, IsAuditLoggingEnabled, IsVatEnabled
            FROM TenantSecuritySettings WHERE TenantId = @TenantId
            """, new { request.TenantId }, cancellationToken: cancellationToken));

        if (row is null)
        {
            row = new TenantSecuritySettingsDto(false, 90, 30, true, true, false);
        }

        return ApiResponse<TenantSecuritySettingsDto>.SuccessResponse(row);
    }
}

public class UpdateTenantSecuritySettingsCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<UpdateTenantSecuritySettingsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateTenantSecuritySettingsCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        using var connection = dbFactory.CreateConnection();
        var p = request.Payload;

        await connection.ExecuteAsync(new CommandDefinition("""
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
            """, new
        {
            request.TenantId,
            p.IsMfaRequired,
            p.PasswordExpiryDays,
            p.SessionTimeoutMinutes,
            p.IsGdprEnabled,
            p.IsAuditLoggingEnabled,
            p.IsVatEnabled
        }, cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Security settings updated.");
    }
}

public class GetRoleTemplatesQueryHandler
    : IRequestHandler<GetRoleTemplatesQuery, ApiResponse<IReadOnlyList<RoleTemplateDto>>>
{
    private static readonly Dictionary<string, string> RoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TENANT_ADMIN"] = "Tenant Admin",
        ["FLEET_MANAGER"] = "Fleet Manager",
        ["DISPATCHER"] = "Dispatcher",
        ["ACCOUNTANT"] = "Accountant",
        ["DRIVER"] = "Driver",
        ["SUPER_ADMIN"] = "Super Admin",
        ["DRIVER_MANAGER"] = "Driver Manager"
    };

    public Task<ApiResponse<IReadOnlyList<RoleTemplateDto>>> Handle(
        GetRoleTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = TenantRolePermissionTemplates.StandardRoles
            .Select(t => new RoleTemplateDto(
                t.RoleCode,
                RoleNames.GetValueOrDefault(t.RoleCode, t.RoleCode.Replace('_', ' ')),
                t.Permissions.Length,
                t.Permissions))
            .ToList();

        return Task.FromResult(ApiResponse<IReadOnlyList<RoleTemplateDto>>.SuccessResponse(templates));
    }
}

public class ApplyRoleTemplateCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<ApplyRoleTemplateCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ApplyRoleTemplateCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var template = TenantRolePermissionTemplates.StandardRoles
            .FirstOrDefault(t => string.Equals(t.RoleCode, request.RoleCode, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(template.RoleCode))
            throw new NotFoundException("Role template", request.RoleCode);

        using var connection = dbFactory.CreateConnection();
        var code = template.RoleCode.ToUpperInvariant();
        var name = code.Replace('_', ' ');

        var roleId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Id FROM Roles WHERE TenantId = @TenantId AND Code = @Code",
            new { request.TenantId, Code = code }, cancellationToken: cancellationToken));

        if (!roleId.HasValue)
        {
            roleId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
                VALUES (@TenantId, @Name, @Code, 1, 1, GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new { request.TenantId, Name = name, Code = code },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM RolePermissions WHERE RoleId = @RoleId",
            new { RoleId = roleId.Value }, cancellationToken: cancellationToken));

        foreach (var perm in template.Permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT @RoleId, p.Id FROM Permissions p WHERE p.PermissionCode = @Code
                """, new { RoleId = roleId.Value, Code = perm }, cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, $"Template '{name}' applied.");
    }
}

// Module Management (Sprint 3)

public class GetTenantModuleOverviewQueryHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<GetTenantModuleOverviewQuery, ApiResponse<TenantModuleOverviewDto>>
{
    public async Task<ApiResponse<TenantModuleOverviewDto>> Handle(
        GetTenantModuleOverviewQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        using var connection = dbFactory.CreateConnection();

        var tenant = await connection.QuerySingleOrDefaultAsync<(string Name, string? PlanName)>(new CommandDefinition("""
            SELECT t.Name, COALESCE(s.PlanName, t.SubscriptionPlan) AS PlanName
            FROM Tenants t
            LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
            WHERE t.Id = @TenantId
            """, new { request.TenantId }, cancellationToken: cancellationToken));
        if (tenant.Name is null) throw new NotFoundException("Tenant", request.TenantId);

        var enabledCodes = (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT m.ModuleCode
            FROM TenantModules tm
            INNER JOIN Modules m ON m.Id = tm.ModuleId
            WHERE tm.TenantId = @TenantId
            """, new { request.TenantId }, cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (enabledCodes.Count == 0)
        {
            var legacyJson = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT EnabledModulesJson FROM Tenants WHERE Id = @TenantId",
                new { request.TenantId }, cancellationToken: cancellationToken));
            if (!string.IsNullOrWhiteSpace(legacyJson))
            {
                try
                {
                    var keys = System.Text.Json.JsonSerializer.Deserialize<string[]>(legacyJson) ?? [];
                    foreach (var code in TenantModuleCatalog.CodesFromLegacyKeys(keys))
                        enabledCodes.Add(code);
                }
                catch { /* ignore malformed legacy json */ }
            }
        }

        var modules = TenantModuleCatalog.All
            .Select(m => new ModuleStatusDto(m.Code, m.Name, enabledCodes.Contains(m.Code)))
            .ToList();

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
            """, new { request.TenantId }, cancellationToken: cancellationToken));

        var licenseLimits = new List<LicenseLimitDto>
        {
            new("Users", limits.UsedUsers, limits.MaxUsers),
            new("Vehicles", limits.UsedVehicles, limits.MaxVehicles),
            new("Drivers", limits.UsedDrivers, limits.MaxDrivers),
            new("Branches", limits.UsedBranches, limits.MaxBranches),
            new("GPS Devices", limits.UsedGps, limits.MaxGpsDevices),
        };

        return ApiResponse<TenantModuleOverviewDto>.SuccessResponse(
            new TenantModuleOverviewDto(request.TenantId, tenant.Name, tenant.PlanName, modules, licenseLimits));
    }
}

public class SetTenantModulesCommandHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope,
    ITenantModuleService tenantModuleService)
    : IRequestHandler<SetTenantModulesCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SetTenantModulesCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        using var connection = dbFactory.CreateConnection();

        var tenantExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Tenants WHERE Id = @TenantId",
            new { request.TenantId }, cancellationToken: cancellationToken));
        if (tenantExists == 0) throw new NotFoundException("Tenant", request.TenantId);

        var validCodes = TenantModuleCatalog.All.Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var moduleCodes = (request.ModuleCodes ?? Array.Empty<string>())
            .Where(c => validCodes.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM TenantModules WHERE TenantId = @TenantId",
            new { request.TenantId }, cancellationToken: cancellationToken));

        foreach (var code in moduleCodes)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TenantModules (TenantId, ModuleId)
                SELECT @TenantId, m.Id FROM Modules m WHERE m.ModuleCode = @Code;
                """, new { request.TenantId, Code = code }, cancellationToken: cancellationToken));
        }

        await tenantModuleService.SyncLegacyJsonAsync(request.TenantId, moduleCodes, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Modules updated.");
    }
}

// Subscription Management (Sprint 4)

public class GetSubscriptionOverviewQueryHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<GetSubscriptionOverviewQuery, ApiResponse<SubscriptionOverviewDto>>
{
    public async Task<ApiResponse<SubscriptionOverviewDto>> Handle(
        GetSubscriptionOverviewQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        using var connection = dbFactory.CreateConnection();

        var subscription = await connection.QuerySingleOrDefaultAsync<SubscriptionDetailDto>(new CommandDefinition("""
            SELECT
                t.Id AS TenantId,
                t.Name AS TenantName,
                COALESCE(s.PlanName, t.SubscriptionPlan) AS PlanName,
                COALESCE(s.Status, N'Active') AS Status,
                COALESCE(s.BillingCycle, N'Monthly') AS BillingCycle,
                s.MonthlyAmount,
                COALESCE(s.CurrencyCode, N'PKR') AS CurrencyCode,
                COALESCE(s.AutoRenew, 1) AS AutoRenew,
                s.SubscriptionStartDate,
                s.SubscriptionEndDate,
                s.TrialEndDate,
                s.MaxUsers, s.MaxVehicles, s.MaxDrivers, s.MaxBranches, s.MaxGpsDevices
            FROM Tenants t
            LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
            WHERE t.Id = @TenantId
            """, new { request.TenantId }, cancellationToken: cancellationToken));

        if (subscription is null) throw new NotFoundException("Tenant", request.TenantId);

        var invoices = (await connection.QueryAsync<InvoiceDto>(new CommandDefinition("""
            SELECT Id, InvoiceNumber, PlanName, Amount, CurrencyCode, Status, IssuedDate, DueDate, PaidDate
            FROM TenantInvoices
            WHERE TenantId = @TenantId
            ORDER BY IssuedDate DESC
            """, new { request.TenantId }, cancellationToken: cancellationToken))).ToList();

        var payments = (await connection.QueryAsync<PaymentDto>(new CommandDefinition("""
            SELECT Id, InvoiceId, Amount, CurrencyCode, PaymentMethod, Status, Reference, PaidAt
            FROM TenantBillingHistory
            WHERE TenantId = @TenantId
            ORDER BY PaidAt DESC
            """, new { request.TenantId }, cancellationToken: cancellationToken))).ToList();

        return ApiResponse<SubscriptionOverviewDto>.SuccessResponse(
            new SubscriptionOverviewDto(subscription, invoices, payments));
    }
}

public class UpdateSubscriptionCommandHandler(IDbConnectionFactory dbFactory, IPlatformScope platformScope)
    : IRequestHandler<UpdateSubscriptionCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        using var connection = dbFactory.CreateConnection();

        var tenantExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Tenants WHERE Id = @TenantId",
            new { request.TenantId }, cancellationToken: cancellationToken));
        if (tenantExists == 0) throw new NotFoundException("Tenant", request.TenantId);

        await EnsureSubscriptionRowAsync(connection, request.TenantId, cancellationToken);

        switch (request.Action)
        {
            case SubscriptionAction.Upgrade:
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE TenantSubscriptions
                    SET PlanName = COALESCE(@PlanName, PlanName),
                        MonthlyAmount = COALESCE(@MonthlyAmount, MonthlyAmount),
                        BillingCycle = COALESCE(@BillingCycle, BillingCycle),
                        Status = N'Active',
                        UpdatedAt = GETUTCDATE()
                    WHERE TenantId = @TenantId;
                    """, new { request.TenantId, request.PlanName, request.MonthlyAmount, request.BillingCycle },
                    cancellationToken: cancellationToken));
                await SyncTenantPlanAsync(connection, request.TenantId, request.PlanName, cancellationToken);
                break;

            case SubscriptionAction.Renew:
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE TenantSubscriptions
                    SET SubscriptionEndDate = DATEADD(year, 1,
                            CASE WHEN SubscriptionEndDate IS NULL OR SubscriptionEndDate < GETUTCDATE()
                                 THEN GETUTCDATE() ELSE SubscriptionEndDate END),
                        Status = N'Active',
                        UpdatedAt = GETUTCDATE()
                    WHERE TenantId = @TenantId;
                    """, new { request.TenantId }, cancellationToken: cancellationToken));
                await GenerateRenewalInvoiceAsync(connection, request.TenantId, cancellationToken);
                break;

            case SubscriptionAction.Suspend:
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE TenantSubscriptions SET Status = N'Suspended', UpdatedAt = GETUTCDATE() WHERE TenantId = @TenantId",
                    new { request.TenantId }, cancellationToken: cancellationToken));
                break;

            case SubscriptionAction.Cancel:
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE TenantSubscriptions SET Status = N'Cancelled', AutoRenew = 0, UpdatedAt = GETUTCDATE() WHERE TenantId = @TenantId",
                    new { request.TenantId }, cancellationToken: cancellationToken));
                break;

            case SubscriptionAction.Reactivate:
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE TenantSubscriptions SET Status = N'Active', UpdatedAt = GETUTCDATE() WHERE TenantId = @TenantId",
                    new { request.TenantId }, cancellationToken: cancellationToken));
                break;
        }

        if (request.AutoRenew.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE TenantSubscriptions SET AutoRenew = @AutoRenew, UpdatedAt = GETUTCDATE() WHERE TenantId = @TenantId",
                new { request.TenantId, AutoRenew = request.AutoRenew.Value }, cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, $"Subscription {request.Action.ToString().ToLowerInvariant()}d.");
    }

    private static async Task EnsureSubscriptionRowAsync(System.Data.IDbConnection connection, int tenantId, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM TenantSubscriptions WHERE TenantId = @TenantId)
            INSERT INTO TenantSubscriptions (TenantId, PlanName, Status, SubscriptionStartDate, SubscriptionEndDate, AutoRenew, BillingCycle, CurrencyCode, MonthlyAmount)
            SELECT @TenantId, COALESCE(t.SubscriptionPlan, N'Enterprise'), N'Active', GETUTCDATE(), DATEADD(year, 1, GETUTCDATE()), 1, N'Monthly', N'PKR', 5000
            FROM Tenants t WHERE t.Id = @TenantId;
            """, new { TenantId = tenantId }, cancellationToken: ct));
    }

    private static async Task SyncTenantPlanAsync(System.Data.IDbConnection connection, int tenantId, string? planName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(planName)) return;
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Tenants SET SubscriptionPlan = @PlanName, UpdatedAt = GETUTCDATE() WHERE Id = @TenantId",
            new { TenantId = tenantId, PlanName = planName }, cancellationToken: ct));
    }

    private static async Task GenerateRenewalInvoiceAsync(System.Data.IDbConnection connection, int tenantId, CancellationToken ct)
    {
        var sub = await connection.QuerySingleOrDefaultAsync<(string? Plan, decimal? Amount, string? Currency)>(
            new CommandDefinition(
                "SELECT PlanName, MonthlyAmount AS Amount, CurrencyCode AS Currency FROM TenantSubscriptions WHERE TenantId = @TenantId",
                new { TenantId = tenantId }, cancellationToken: ct));

        var amount = sub.Amount ?? 5000m;
        var currency = string.IsNullOrWhiteSpace(sub.Currency) ? "PKR" : sub.Currency;
        var invoiceNumber = $"INV-{tenantId:D4}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantInvoices (TenantId, InvoiceNumber, PlanName, Amount, CurrencyCode, Status, IssuedDate, DueDate, CreatedAt)
            VALUES (@TenantId, @InvoiceNumber, @Plan, @Amount, @Currency, N'Pending', GETUTCDATE(), DATEADD(day, 14, GETUTCDATE()), GETUTCDATE());
            """, new { TenantId = tenantId, InvoiceNumber = invoiceNumber, Plan = sub.Plan, Amount = amount, Currency = currency },
            cancellationToken: ct));
    }
}
