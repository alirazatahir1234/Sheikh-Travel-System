using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Tenants;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence;

public class TenantProvisioningService(
    IDbConnectionFactory dbFactory,
    IPasswordHasher passwordHasher) : ITenantProvisioningService
{
    private static readonly (string Name, string Code)[] DefaultRoles =
    [
        ("Super Admin", "SUPER_ADMIN"),
        ("Tenant Admin", "TENANT_ADMIN"),
        ("Fleet Manager", "FLEET_MANAGER"),
        ("Dispatcher", "DISPATCHER"),
        ("Driver Manager", "DRIVER_MANAGER"),
        ("Accountant", "ACCOUNTANT"),
        ("Driver", "DRIVER"),
    ];

    public async Task<int> ProvisionAsync(ProvisionTenantCommand request, int? createdByUserId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var tenantId = await ProvisionCoreAsync(
                connection, transaction, request, createdByUserId, cancellationToken);
            transaction.Commit();
            return tenantId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task<int> ProvisionCoreAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        ProvisionTenantCommand request,
        int? createdByUserId,
        CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        var moduleCodes = request.ModuleCodes is { Count: > 0 }
            ? request.ModuleCodes
            : TenantModuleCatalog.DefaultModuleCodes;
        var legacyJson = TenantModuleCatalog.SerializeLegacyKeys(
            TenantModuleCatalog.LegacyKeysFromCodes(moduleCodes));

        var tenantId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Tenants (Name, Slug, Code, IsActive, TenantType, IndustryType, StorageModel, Status, DataRegion,
                CreatedByUserId, EnabledModulesJson, SubscriptionPlan, CreatedAt)
            VALUES (@Name, @Slug, @Code, 1, @TenantType, @IndustryType, @StorageModel, @Status, @DataRegion,
                @CreatedBy, @ModulesJson, @PlanName, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Code = request.Code?.Trim(),
            request.TenantType,
            request.IndustryType,
            request.StorageModel,
            request.Status,
            request.DataRegion,
            CreatedBy = createdByUserId,
            ModulesJson = legacyJson,
            request.PlanName
        }, transaction: transaction, cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            var code = $"SK-{tenantId:D4}";
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Tenants SET Code = @Code WHERE Id = @TenantId",
                new { TenantId = tenantId, Code = code },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantSubscriptions (TenantId, PlanName, MaxUsers, MaxVehicles, MaxDrivers, MaxBranches, MaxGpsDevices,
                Status, SubscriptionStartDate)
            VALUES (@TenantId, @PlanName, @MaxUsers, @MaxVehicles, @MaxDrivers, @MaxBranches, @MaxGpsDevices,
                @Status, GETUTCDATE());
            """, new
        {
            TenantId = tenantId,
            request.PlanName,
            request.MaxUsers,
            request.MaxVehicles,
            request.MaxDrivers,
            request.MaxBranches,
            request.MaxGpsDevices,
            request.Status
        }, transaction: transaction, cancellationToken: cancellationToken));

        foreach (var code in moduleCodes)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TenantModules (TenantId, ModuleId)
                SELECT @TenantId, m.Id FROM Modules m WHERE m.ModuleCode = @Code;
                """, new { TenantId = tenantId, Code = code },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantBranding (TenantId, LogoUrl, PrimaryColor, Website, SupportEmail, Country, CurrencyCode, TimeZone)
            VALUES (@TenantId, @LogoUrl, @PrimaryColor, @Website, @SupportEmail, @Country, @CurrencyCode, @TimeZone);
            """, new
        {
            TenantId = tenantId,
            request.LogoUrl,
            request.PrimaryColor,
            request.Website,
            request.SupportEmail,
            request.Country,
            request.CurrencyCode,
            request.TimeZone
        }, transaction: transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantSecuritySettings (TenantId, IsMfaRequired, PasswordExpiryDays, SessionTimeoutMinutes,
                IsGdprEnabled, IsAuditLoggingEnabled, IsVatEnabled)
            VALUES (@TenantId, @IsMfaRequired, @PasswordExpiryDays, @SessionTimeoutMinutes,
                @IsGdprEnabled, @IsAuditLoggingEnabled, @IsVatEnabled);
            """, new
        {
            TenantId = tenantId,
            request.IsMfaRequired,
            request.PasswordExpiryDays,
            request.SessionTimeoutMinutes,
            request.IsGdprEnabled,
            request.IsAuditLoggingEnabled,
            request.IsVatEnabled
        }, transaction: transaction, cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(request.BillingContactName)
            || !string.IsNullOrWhiteSpace(request.BillingEmail)
            || !string.IsNullOrWhiteSpace(request.CompanyTRN))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TenantBilling (TenantId, BillingContactName, BillingEmail, BillingAddress, CompanyTRN)
                VALUES (@TenantId, @BillingContactName, @BillingEmail, @BillingAddress, @CompanyTRN);
                """, new
            {
                TenantId = tenantId,
                request.BillingContactName,
                request.BillingEmail,
                request.BillingAddress,
                request.CompanyTRN
            }, transaction: transaction, cancellationToken: cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(request.GpsProviderName))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TenantGpsSettings (TenantId, ProviderName)
                VALUES (@TenantId, @ProviderName);
                """, new { TenantId = tenantId, ProviderName = request.GpsProviderName },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        var hash = passwordHasher.Hash(request.AdminPassword);
        var adminUserId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Users (TenantId, FullName, Email, PasswordHash, Phone, Role, IsActive, CreatedAt, IsDeleted)
            VALUES (@TenantId, @Name, @Email, @Hash, @Phone, @Role, 1, GETUTCDATE(), 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            Name = request.AdminFullName.Trim(),
            Email = request.AdminEmail.Trim(),
            Hash = hash,
            Phone = request.AdminMobile?.Trim() ?? string.Empty,
            Role = (int)UserRole.Admin
        }, transaction: transaction, cancellationToken: cancellationToken));

        await SeedRolesAsync(connection, transaction, tenantId, cancellationToken);
        await TenantRolePermissionSeeder.SeedSystemRolePermissionsForTenantAsync(
            connection, tenantId, cancellationToken, transaction);
        await AssignAdminRoleAsync(connection, transaction, tenantId, adminUserId, cancellationToken);

        if (request.GenerateOrganizationStructure)
            await LinkAdminToOrganizationAsync(
                connection, transaction, tenantId, adminUserId, request, cancellationToken);

        return tenantId;
    }

    private static async Task SeedRolesAsync(
        IDbConnection connection, IDbTransaction transaction, int tenantId, CancellationToken ct)
    {
        foreach (var (name, code) in DefaultRoles)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM Roles WHERE TenantId = @TenantId AND Code = @Code)
                INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
                VALUES (@TenantId, @Name, @Code, 1, 1, GETUTCDATE());
                """, new { TenantId = tenantId, Name = name, Code = code },
                transaction: transaction,
                cancellationToken: ct));
        }
    }

    private static async Task AssignAdminRoleAsync(
        IDbConnection connection, IDbTransaction transaction, int tenantId, int adminUserId, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO UserRoles (UserId, RoleId)
            SELECT @UserId, r.Id
            FROM Roles r
            WHERE r.TenantId = @TenantId AND r.Code = 'TENANT_ADMIN'
              AND NOT EXISTS (SELECT 1 FROM UserRoles ur WHERE ur.UserId = @UserId AND ur.RoleId = r.Id);
            """, new { TenantId = tenantId, UserId = adminUserId },
            transaction: transaction,
            cancellationToken: ct));
    }

    private static async Task LinkAdminToOrganizationAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int tenantId,
        int adminUserId,
        ProvisionTenantCommand request,
        CancellationToken ct)
    {
        var branchName = request.DefaultBranchName?.Trim() ?? "Main Operations Center";
        var headOffice = request.HeadOfficeName?.Trim() ?? "Head Office";

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Branches (TenantId, BranchCode, Name, Address, City, Country, TimeZone, CurrencyCode, Status, IsGpsEnabled, IsActive, CreatedAt)
            VALUES (@TenantId, N'HQ-001', @Name, @HeadOffice, @City, @Country, @TimeZone, @Currency, 1, 1, 1, GETUTCDATE());
            """, new
        {
            TenantId = tenantId,
            Name = branchName,
            HeadOffice = headOffice,
            City = request.Country == "United Arab Emirates" ? "Dubai" : null,
            request.Country,
            request.TimeZone,
            Currency = request.CurrencyCode
        }, transaction: transaction, cancellationToken: ct));

        var departments = (request.DefaultDepartments ?? "Operations,Finance,Fleet,HR")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var dept in departments)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM Departments WHERE TenantId = @TenantId AND Name = @Name)
                INSERT INTO Departments (TenantId, Name, IsActive, CreatedAt)
                VALUES (@TenantId, @Name, 1, GETUTCDATE());
                """, new { TenantId = tenantId, Name = dept },
                transaction: transaction,
                cancellationToken: ct));
        }

        var branchId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 Id FROM Branches WHERE TenantId = @TenantId ORDER BY Id",
            new { TenantId = tenantId },
            transaction: transaction,
            cancellationToken: ct));

        var deptId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 Id FROM Departments WHERE TenantId = @TenantId AND Name = N'Operations'",
            new { TenantId = tenantId },
            transaction: transaction,
            cancellationToken: ct));

        if (branchId.HasValue || deptId.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Users SET BranchId = COALESCE(@BranchId, BranchId), DepartmentId = COALESCE(@DeptId, DepartmentId)
                WHERE Id = @UserId
                """, new { UserId = adminUserId, BranchId = branchId, DeptId = deptId },
                transaction: transaction,
                cancellationToken: ct));
        }
    }
}
