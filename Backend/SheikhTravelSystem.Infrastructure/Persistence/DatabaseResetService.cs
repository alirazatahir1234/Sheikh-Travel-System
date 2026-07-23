using System.Data;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence;

/// <summary>
/// Development/QA-only database reset. Deletes all tenant operational data while
/// preserving migration history, platform navigation, permission catalog, and master lookups.
/// </summary>
public sealed class DatabaseResetService(
    IDbConnectionFactory dbFactory,
    IDatabaseSeeder databaseSeeder,
    ITenantRoleSeedService tenantRoleSeedService,
    IPasswordHasher passwordHasher,
    IHostEnvironment environment,
    ILogger<DatabaseResetService> logger) : IDatabaseResetService
{
    private const string DefaultAdminEmail = "admin@sheikhtravel.com";
    private const string DefaultAdminPassword = "Pass@123";

    /// <summary>
    /// Tables whose rows are never deleted during reset.
    /// </summary>
    private static readonly HashSet<string> PreservedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SchemaMigrationHistory",
        "Permissions",
        "PlatformModules",
        "PlatformMenus",
        "Modules",
        "TrackerBrands",
        "TrackerModels",
        "FleetStatusCodes",
    };

    public async Task<DatabaseResetResult> ResetAsync(
        int performedByUserId,
        string? ipAddress,
        string? machineName,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment() && !environment.IsStaging())
            throw new InvalidOperationException("Database reset is only allowed in Development or Staging.");

        using var connection = dbFactory.CreateConnection();
        connection.Open();

        var stats = await CollectStatsAsync(connection, cancellationToken);

        logger.LogWarning(
            "Database reset initiated by user {UserId} from {IpAddress} on {Machine} in {Environment}",
            performedByUserId, ipAddress, machineName, environment.EnvironmentName);

        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await DisableForeignKeysAsync(connection, transaction, cancellationToken);
            var clearedTables = await ClearOperationalTablesAsync(connection, transaction, cancellationToken);
            await EnableForeignKeysAsync(connection, transaction, cancellationToken);
            await ReseedIdentitiesAsync(connection, transaction, clearedTables, cancellationToken);
            await RestoreDefaultTenantAsync(connection, transaction, cancellationToken);
            await RestoreDefaultRolesAsync(connection, transaction, cancellationToken);
            await RestoreSystemAdminAsync(connection, transaction, cancellationToken);
            await AssignSuperAdminRoleAsync(connection, transaction, cancellationToken);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        await tenantRoleSeedService.SeedSystemRolePermissionsAsync(1, cancellationToken);
        await tenantRoleSeedService.SeedSuperAdminPermissionsAsync(1, cancellationToken);
        await databaseSeeder.SeedAsync(cancellationToken);

        logger.LogWarning(
            "Database reset completed by user {UserId}. Deleted companies={Companies}, users={Users}, trips={Trips}, vehicles={Vehicles}",
            performedByUserId, stats.Tenants, stats.Users, stats.Trips, stats.Vehicles);

        return new DatabaseResetResult(
            Success: true,
            Message: "Database successfully reset.",
            DeletedCompanies: stats.Tenants,
            DeletedUsers: stats.Users,
            DeletedTrips: stats.Trips,
            DeletedVehicles: stats.Vehicles,
            DeletedBookings: stats.Bookings,
            DeletedDrivers: stats.Drivers,
            DeletedCustomers: stats.Customers,
            ReseededTables: ["Tenants", "Roles", "Users", "DemoData"]);
    }

    private static async Task<(int Tenants, int Users, int Trips, int Vehicles, int Bookings, int Drivers, int Customers)> CollectStatsAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        async Task<int> Count(string table) =>
            await TableExistsAsync(connection, table, cancellationToken)
                ? await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition($"SELECT COUNT(*) FROM [{table}]", cancellationToken: cancellationToken))
                : 0;

        return (
            await Count("Tenants"),
            await Count("Users"),
            await TableExistsAsync(connection, "Trips", cancellationToken) ? await Count("Trips") : 0,
            await Count("Vehicles"),
            await Count("Bookings"),
            await Count("Drivers"),
            await Count("Customers"));
    }

    private static async Task DisableForeignKeysAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'",
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task EnableForeignKeysAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'",
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<string>> ClearOperationalTablesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var tables = (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'dbo'
            ORDER BY TABLE_NAME
            """, transaction: transaction, cancellationToken: cancellationToken))).ToList();

        var cleared = new List<string>();
        foreach (var table in tables)
        {
            if (PreservedTables.Contains(table))
                continue;

            await connection.ExecuteAsync(new CommandDefinition(
                $"DELETE FROM [{table}]",
                transaction: transaction,
                cancellationToken: cancellationToken));
            cleared.Add(table);
        }

        return cleared;
    }

    private async Task ReseedIdentitiesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        IReadOnlyList<string> tables,
        CancellationToken cancellationToken)
    {
        foreach (var table in tables)
        {
            var hasIdentity = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*)
                FROM sys.identity_columns ic
                INNER JOIN sys.tables t ON t.object_id = ic.object_id
                WHERE t.name = @Table
                """, new { Table = table }, transaction: transaction, cancellationToken: cancellationToken));

            if (hasIdentity == 0)
                continue;

            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    $"DBCC CHECKIDENT ('[{table}]', RESEED, 0)",
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }
            catch (Exception ex)
            {
                // Non-fatal — some tables may not support reseed in the current context.
                logger.LogDebug(ex, "Identity reseed skipped for {Table}", table);
            }
        }
    }

    private static async Task RestoreDefaultTenantAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string modulesJson =
            "[\"dashboard\",\"bookings\",\"vehicles\",\"drivers\",\"customers\",\"routes\",\"fuel-logs\",\"maintenance\",\"gps-tracking\",\"payments\",\"reports\",\"driver-allowance-rules\",\"users\",\"audit-logs\"]";

        await connection.ExecuteAsync(new CommandDefinition("""
            SET IDENTITY_INSERT Tenants ON;
            INSERT INTO Tenants (Id, Name, Slug, IsActive, EnabledModulesJson, CreatedAt)
            VALUES (1, N'Sheikh Travel', N'default', 1, @ModulesJson, GETUTCDATE());
            SET IDENTITY_INSERT Tenants OFF;
            """, new { ModulesJson = modulesJson },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM TenantModules tm INNER JOIN Modules m ON m.Id = tm.ModuleId WHERE tm.TenantId = 1)
            BEGIN
                INSERT INTO TenantModules (TenantId, ModuleId)
                SELECT 1, m.Id FROM Modules m;
            END
            """, transaction: transaction, cancellationToken: cancellationToken));
    }

    private static async Task RestoreDefaultRolesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var roleDefs = new (string Name, string Code)[]
        {
            ("Super Admin", "SUPER_ADMIN"),
            ("Tenant Admin", "TENANT_ADMIN"),
            ("Fleet Manager", "FLEET_MANAGER"),
            ("Dispatcher", "DISPATCHER"),
            ("Driver Manager", "DRIVER_MANAGER"),
            ("Accountant", "ACCOUNTANT"),
            ("Driver", "DRIVER"),
        };

        foreach (var (name, code) in roleDefs)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM Roles WHERE TenantId = 1 AND Code = @Code)
                INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
                VALUES (1, @Name, @Code, 1, 1, GETUTCDATE());
                """, new { Name = name, Code = code },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }
    }

    private async Task RestoreSystemAdminAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var hash = passwordHasher.Hash(DefaultAdminPassword);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Users (TenantId, FullName, Email, PasswordHash, Phone, Role, IsActive, CreatedAt, CreatedBy, IsDeleted)
            VALUES (1, N'System Admin', @Email, @PasswordHash, N'03001234567', @Role, 1, GETUTCDATE(), N'system-reset', 0);
            """, new
        {
            Email = DefaultAdminEmail,
            PasswordHash = hash,
            Role = (int)UserRole.Admin
        }, transaction: transaction, cancellationToken: cancellationToken));
    }

    private static async Task AssignSuperAdminRoleAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO UserRoles (UserId, RoleId)
            SELECT u.Id, r.Id
            FROM Users u
            INNER JOIN Roles r ON r.TenantId = u.TenantId AND r.Code = 'SUPER_ADMIN'
            WHERE u.Email = @Email AND u.IsDeleted = 0
              AND NOT EXISTS (SELECT 1 FROM UserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);
            """, new { Email = DefaultAdminEmail },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<bool> TableExistsAsync(
        IDbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @Table",
            new { Table = table },
            cancellationToken: cancellationToken)) > 0;
    }
}
