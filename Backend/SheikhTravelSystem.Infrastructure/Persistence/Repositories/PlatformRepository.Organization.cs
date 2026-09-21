using Dapper;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<BranchDto>(new CommandDefinition(
            PlatformBranchSql.SelectColumns + " WHERE b.TenantId = @TenantId ORDER BY b.Name",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<BranchDto?> GetBranchByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<BranchDto>(new CommandDefinition(
            PlatformBranchSql.SelectColumns + " WHERE b.Id = @Id AND b.TenantId = @TenantId",
            new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<int> CreateBranchAsync(int tenantId, BranchUpsertPayload payload, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await ValidateBranchAsync(connection, tenantId, null, payload, cancellationToken);
        var isActive = payload.Status == (int)BranchStatus.Active;
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Branches (
                TenantId, ParentBranchId, BranchCode, Name, BranchType, BranchManagerUserId,
                Phone, Email, Address, City, Country, TimeZone, CurrencyCode, Status, IsGpsEnabled, IsActive, CreatedAt)
            VALUES (
                @TenantId, @ParentBranchId, @BranchCode, @Name, @BranchType, @BranchManagerUserId,
                @Phone, @Email, @Address, @City, @Country, @TimeZone, @CurrencyCode, @Status, @IsGpsEnabled, @IsActive, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, ToBranchParams(tenantId, null, payload, isActive),
            cancellationToken: cancellationToken));
    }

    public async Task<int> UpdateBranchAsync(int tenantId, int id, BranchUpsertPayload payload, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await ValidateBranchAsync(connection, tenantId, id, payload, cancellationToken);
        var isActive = payload.Status == (int)BranchStatus.Active;
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Branches SET
                ParentBranchId = @ParentBranchId, BranchCode = @BranchCode, Name = @Name, BranchType = @BranchType,
                BranchManagerUserId = @BranchManagerUserId, Phone = @Phone, Email = @Email, Address = @Address,
                City = @City, Country = @Country, TimeZone = @TimeZone, CurrencyCode = @CurrencyCode,
                Status = @Status, IsGpsEnabled = @IsGpsEnabled, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, ToBranchParams(tenantId, id, payload, isActive),
            cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteBranchAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Branches WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task UnassignDepartmentsFromBranchAsync(int tenantId, int branchId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Departments SET BranchId = NULL WHERE BranchId = @BranchId AND TenantId = @TenantId",
            new { BranchId = branchId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
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
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task EnsureDepartmentHeadExistsAsync(int tenantId, int? headUserId, CancellationToken cancellationToken = default)
    {
        if (!headUserId.HasValue) return;
        using var connection = dbFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Users WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = headUserId.Value, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (exists == 0)
            throw new NotFoundException("Department head user", headUserId.Value);
    }

    public async Task EnsureBranchExistsAsync(int tenantId, int branchId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var branchExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Branches WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = branchId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (branchExists == 0) throw new NotFoundException("Branch", branchId);
    }

    public async Task<int> CreateDepartmentAsync(
        int tenantId, string name, int? departmentHeadUserId, int? branchId = null, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (branchId.HasValue)
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO Departments (TenantId, BranchId, Name, DepartmentHeadUserId, IsActive, CreatedAt)
                VALUES (@TenantId, @BranchId, @Name, @DepartmentHeadUserId, 1, GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
            {
                TenantId = tenantId,
                BranchId = branchId,
                Name = name,
                DepartmentHeadUserId = departmentHeadUserId
            }, cancellationToken: cancellationToken));
        }

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Departments (TenantId, Name, DepartmentHeadUserId, IsActive, CreatedAt)
            VALUES (@TenantId, @Name, @DepartmentHeadUserId, 1, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            Name = name,
            DepartmentHeadUserId = departmentHeadUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<int> UpdateDepartmentAsync(
        int tenantId, int id, string name, int? departmentHeadUserId, bool isActive,
        int? branchId = null, bool includeBranchId = false, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (includeBranchId)
        {
            return await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Departments SET Name = @Name, BranchId = @BranchId, DepartmentHeadUserId = @DepartmentHeadUserId,
                    IsActive = @IsActive, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND TenantId = @TenantId
                """, new
            {
                Id = id,
                TenantId = tenantId,
                Name = name,
                BranchId = branchId,
                DepartmentHeadUserId = departmentHeadUserId,
                IsActive = isActive
            }, cancellationToken: cancellationToken));
        }

        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Departments SET Name = @Name, DepartmentHeadUserId = @DepartmentHeadUserId,
                IsActive = @IsActive, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            DepartmentHeadUserId = departmentHeadUserId,
            IsActive = isActive
        }, cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteDepartmentAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Departments WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<int> MoveDepartmentAsync(int tenantId, int departmentId, int? newBranchId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Departments SET BranchId = @NewBranchId, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = departmentId, TenantId = tenantId, NewBranchId = newBranchId },
            cancellationToken: cancellationToken));
    }

    public async Task<string?> GetTenantNameAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT Name FROM Tenants WHERE Id = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<OrganizationBranchRow>> GetOrganizationBranchesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<OrganizationBranchRow>(new CommandDefinition("""
                SELECT Id, ParentBranchId, BranchCode, Name, BranchType, City, Country, IsActive, Status
                FROM Branches WHERE TenantId = @TenantId ORDER BY Name
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<OrganizationDepartmentRow>> GetOrganizationDepartmentsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<OrganizationDepartmentRow>(new CommandDefinition("""
                SELECT d.Id, d.BranchId, d.Name,
                       h.FullName AS DepartmentHeadName,
                       (SELECT COUNT(*) FROM Users u WHERE u.DepartmentId = d.Id AND u.TenantId = d.TenantId AND u.IsDeleted = 0) AS StaffCount,
                       d.IsActive
                FROM Departments d
                LEFT JOIN Users h ON h.Id = d.DepartmentHeadUserId AND h.IsDeleted = 0
                WHERE d.TenantId = @TenantId
                ORDER BY d.Name
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private static object ToBranchParams(int tenantId, int? id, BranchUpsertPayload payload, bool isActive) => new
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

    private static async Task ValidateBranchAsync(
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
