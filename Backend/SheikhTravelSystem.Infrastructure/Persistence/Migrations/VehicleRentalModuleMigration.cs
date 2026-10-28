using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Creates rental tables, activates RENTAL module, seeds permissions and ERP menu.
/// </summary>
public static class VehicleRentalModuleMigration
{
    private static readonly (string Module, string Code, string Desc)[] Permissions =
    [
        ("Rental", RentalPermissions.View, "View rental bookings, rates, and availability"),
        ("Rental", RentalPermissions.Manage, "Create and manage rental bookings, pickup/return, deposits, payments"),
        ("Rental", RentalPermissions.Rates, "Configure rental rates"),
    ];

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.RentalRates', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.RentalRates (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RentalRates PRIMARY KEY,
                    TenantId INT NOT NULL,
                    VehicleId INT NULL,
                    RateType NVARCHAR(20) NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    DepositAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_RentalRates_Deposit DEFAULT (0),
                    Currency NVARCHAR(10) NOT NULL CONSTRAINT DF_RentalRates_Currency DEFAULT (N'PKR'),
                    IsActive BIT NOT NULL CONSTRAINT DF_RentalRates_IsActive DEFAULT (1),
                    Notes NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_RentalRates_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_RentalRates_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_RentalRates_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
                );
                CREATE INDEX IX_RentalRates_Tenant_Vehicle_Type
                    ON dbo.RentalRates (TenantId, VehicleId, RateType) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.RentalBookings', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.RentalBookings (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RentalBookings PRIMARY KEY,
                    TenantId INT NOT NULL,
                    RentalNumber NVARCHAR(40) NOT NULL,
                    CustomerId INT NOT NULL,
                    VehicleId INT NOT NULL,
                    VehicleCategory NVARCHAR(100) NULL,
                    RateId INT NULL,
                    RateType NVARCHAR(20) NOT NULL,
                    RateAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_RentalBookings_RateAmount DEFAULT (0),
                    StartAt DATETIME2 NOT NULL,
                    EndAt DATETIME2 NOT NULL,
                    PickupLocation NVARCHAR(200) NULL,
                    ReturnLocation NVARCHAR(200) NULL,
                    DiscountAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_RentalBookings_Discount DEFAULT (0),
                    TotalAmount DECIMAL(18,2) NOT NULL,
                    DepositAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_RentalBookings_Deposit DEFAULT (0),
                    DepositHeld DECIMAL(18,2) NOT NULL CONSTRAINT DF_RentalBookings_DepositHeld DEFAULT (0),
                    DepositRefunded DECIMAL(18,2) NOT NULL CONSTRAINT DF_RentalBookings_DepositRefunded DEFAULT (0),
                    DamageCharges DECIMAL(18,2) NOT NULL CONSTRAINT DF_RentalBookings_Damage DEFAULT (0),
                    DamageNotes NVARCHAR(1000) NULL,
                    Status NVARCHAR(20) NOT NULL,
                    DepositStatus NVARCHAR(30) NOT NULL,
                    PaymentStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_RentalBookings_PayStatus DEFAULT (N'Unpaid'),
                    PaidAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_RentalBookings_Paid DEFAULT (0),
                    PaymentMethod NVARCHAR(50) NULL,
                    PaymentReference NVARCHAR(200) NULL,
                    LinkedPaymentId INT NULL,
                    PickedUpAt DATETIME2 NULL,
                    PickupOdometer INT NULL,
                    PickupFuelLevel NVARCHAR(40) NULL,
                    PickupNotes NVARCHAR(1000) NULL,
                    ReturnedAt DATETIME2 NULL,
                    ReturnOdometer INT NULL,
                    ReturnFuelLevel NVARCHAR(40) NULL,
                    ReturnNotes NVARCHAR(1000) NULL,
                    AgreementText NVARCHAR(MAX) NULL,
                    AgreementSignedAt DATETIME2 NULL,
                    AgreementSignedBy NVARCHAR(200) NULL,
                    Notes NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_RentalBookings_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_RentalBookings_IsDeleted DEFAULT (0),
                    CONSTRAINT UQ_RentalBookings_Tenant_Number UNIQUE (TenantId, RentalNumber),
                    CONSTRAINT FK_RentalBookings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
                );
                CREATE INDEX IX_RentalBookings_Tenant_Status
                    ON dbo.RentalBookings (TenantId, Status) WHERE IsDeleted = 0;
                CREATE INDEX IX_RentalBookings_Tenant_Vehicle_Window
                    ON dbo.RentalBookings (TenantId, VehicleId, StartAt, EndAt) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.RentalPayments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.RentalPayments (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RentalPayments PRIMARY KEY,
                    TenantId INT NOT NULL,
                    RentalBookingId INT NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    PaymentMethod NVARCHAR(50) NOT NULL,
                    Status NVARCHAR(20) NOT NULL,
                    PaymentDate DATETIME2 NOT NULL CONSTRAINT DF_RentalPayments_Date DEFAULT (SYSUTCDATETIME()),
                    TransactionReference NVARCHAR(200) NULL,
                    Notes NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_RentalPayments_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_RentalPayments_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_RentalPayments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_RentalPayments_Bookings FOREIGN KEY (RentalBookingId) REFERENCES dbo.RentalBookings(Id)
                );
                CREATE INDEX IX_RentalPayments_Booking
                    ON dbo.RentalPayments (TenantId, RentalBookingId) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.Modules', N'U') IS NOT NULL
            BEGIN
                UPDATE Modules
                SET Status = N'Active', Route = N'/rental',
                    DisplayName = COALESCE(NULLIF(LTRIM(RTRIM(DisplayName)), ''), N'Rental'),
                    Name = COALESCE(NULLIF(LTRIM(RTRIM(Name)), ''), N'Vehicle Rental')
                WHERE ModuleCode = N'RENTAL';
            END

            IF OBJECT_ID(N'dbo.TenantFeatures', N'U') IS NOT NULL
            BEGIN
                UPDATE TenantFeatures SET IsEnabled = 1, LastModified = SYSUTCDATETIME()
                WHERE FeatureKey = N'rental' AND IsEnabled = 0;
            END
            """, cancellationToken: cancellationToken));

        foreach (var (module, code, desc) in Permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
                AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (@Module, @Code, @Desc);
                """, new { Module = module, Code = code, Desc = desc },
                cancellationToken: cancellationToken));
        }

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "SUPER_ADMIN", RentalPermissions.All, cancellationToken);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "TENANT_ADMIN", RentalPermissions.All, cancellationToken);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "OWNER", RentalPermissions.All, cancellationToken);

        // Platform menu under fleet operations style group
        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformMenus'
            ) AND EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformModules'
            ) THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken)) == 1)
        {
            const string platformModuleKey = "vehicle-rental";
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = @ModuleKey)
                BEGIN
                    INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible,
                        DisplayName, Description, Visible)
                    VALUES (N'Vehicle Rental', @ModuleKey, N'car_rental', 35, 1,
                        N'Vehicle Rental', N'Rental bookings, rates, and availability', 1);
                END
                """, new { ModuleKey = platformModuleKey }, cancellationToken: cancellationToken));

            var menus = new (string Name, string Route, string Icon, string Perm, int Sort, string Display)[]
            {
                ("Rentals", "/rental", "car_rental", RentalPermissions.View, 10, "Rentals"),
                ("Rental Rates", "/rental/rates", "payments", RentalPermissions.Rates, 20, "Rental Rates"),
                ("Availability", "/rental/availability", "event_available", RentalPermissions.View, 30, "Availability"),
            };

            foreach (var m in menus)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE pm SET
                        pm.ModuleId = mod.Id,
                        pm.ParentId = NULL,
                        pm.Name = @Name,
                        pm.Route = @Route,
                        pm.Icon = @Icon,
                        pm.PermissionCode = @Perm,
                        pm.SortOrder = @Sort,
                        pm.IsActive = 1,
                        pm.Visible = 1,
                        pm.DisplayName = @Display,
                        pm.Category = N'Fleet',
                        pm.ModuleKey = N'RENTAL',
                        pm.UpdatedAt = SYSUTCDATETIME()
                    FROM PlatformMenus pm
                    CROSS JOIN PlatformModules mod
                    WHERE mod.ModuleKey = @PlatformModule
                      AND pm.Route = @Route;

                    IF @@ROWCOUNT = 0
                    INSERT INTO PlatformMenus (
                        ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                        DisplayName, Description, Category, Visible, FeatureKey, ModuleKey,
                        IsMobileSupported, UpdatedAt)
                    SELECT mod.Id, NULL, @Name, @Route, @Icon, @Perm, @Sort, 1,
                           @Display, NULL, N'Fleet', 1, N'rental', N'RENTAL', 1, SYSUTCDATETIME()
                    FROM PlatformModules mod
                    WHERE mod.ModuleKey = @PlatformModule
                      AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.Route = @Route);
                    """,
                    new
                    {
                        PlatformModule = platformModuleKey,
                        m.Name,
                        m.Route,
                        m.Icon,
                        m.Perm,
                        m.Sort,
                        m.Display
                    },
                    cancellationToken: cancellationToken));
            }
        }

        logger.LogInformation("VehicleRentalModuleMigration applied (tables + RENTAL Active + permissions).");
    }
}
