using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// TravelNexus Phase 4: hotel catalog, Umrah/Hajj packages (stays, prices), groups, pilgrims (group members), group
/// hotel stays, rooms, room assignments, transport legs and group history; group number prefix; Phase 4 permissions
/// and menu rows. Additive and idempotent.
/// </summary>
public static class TravelNexusUmrahMigration
{
    private static readonly (string Code, string Desc)[] PermissionRows =
    [
        (TravelPermissions.PackageView, "View Umrah/Hajj packages and the hotel catalog"),
        (TravelPermissions.PackageManage, "Create and publish packages; manage the hotel catalog"),
        (TravelPermissions.GroupView, "View Umrah groups, pilgrims, rooms and transport"),
        (TravelPermissions.GroupManage, "Manage Umrah groups, pilgrims, hotel stays, rooms and transport"),
        (TravelPermissions.GroupManifest, "Generate pilgrim manifests (contains passport data; audited)"),
    ];

    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'dbo.TravelSettings', N'GroupPrefix') IS NULL
                ALTER TABLE dbo.TravelSettings ADD GroupPrefix NVARCHAR(10) NOT NULL
                    CONSTRAINT DF_TravelSettings_GroupPrefix DEFAULT (N'GRP');

            IF OBJECT_ID(N'dbo.TravelHotels', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelHotels (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelHotels PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    City NVARCHAR(20) NOT NULL,
                    CountryCode CHAR(2) NOT NULL CONSTRAINT DF_TravelHotels_Country DEFAULT ('SA'),
                    StarRating TINYINT NULL,
                    DistanceToHaramMeters INT NULL,
                    Address NVARCHAR(300) NULL,
                    Phone NVARCHAR(30) NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelHotels_IsActive DEFAULT (1),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelHotels_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelHotels_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelHotels_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelHotels_Country FOREIGN KEY (CountryCode) REFERENCES dbo.TravelCountries(Code),
                    CONSTRAINT CK_TravelHotels_City CHECK (City IN (N'Makkah', N'Madinah', N'Jeddah', N'Taif', N'Other')),
                    CONSTRAINT CK_TravelHotels_Stars CHECK (StarRating IS NULL OR StarRating BETWEEN 1 AND 5),
                    CONSTRAINT CK_TravelHotels_Distance CHECK (DistanceToHaramMeters IS NULL OR DistanceToHaramMeters >= 0)
                );
                CREATE UNIQUE INDEX UX_TravelHotels_Tenant_Name_City ON dbo.TravelHotels (TenantId, Name, City) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelPackages', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelPackages (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelPackages PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Code NVARCHAR(30) NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    PackageType NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelPackages_Type DEFAULT (N'Umrah'),
                    DurationNights INT NOT NULL,
                    NightsMakkah INT NOT NULL CONSTRAINT DF_TravelPackages_Makkah DEFAULT (0),
                    NightsMadinah INT NOT NULL CONSTRAINT DF_TravelPackages_Madinah DEFAULT (0),
                    VisaTypeId INT NULL,
                    Inclusions NVARCHAR(2000) NULL,
                    Exclusions NVARCHAR(2000) NULL,
                    CurrencyCode CHAR(3) NOT NULL,
                    Status NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelPackages_Status DEFAULT (N'Draft'),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelPackages_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelPackages_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelPackages_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelPackages_VisaType FOREIGN KEY (VisaTypeId) REFERENCES dbo.TravelVisaTypes(Id),
                    CONSTRAINT CK_TravelPackages_Type CHECK (PackageType IN (N'Umrah', N'Hajj')),
                    CONSTRAINT CK_TravelPackages_Status CHECK (Status IN (N'Draft', N'Published', N'Archived')),
                    CONSTRAINT CK_TravelPackages_Nights CHECK (DurationNights BETWEEN 1 AND 90 AND NightsMakkah >= 0 AND NightsMadinah >= 0
                        AND NightsMakkah + NightsMadinah <= DurationNights)
                );
                CREATE UNIQUE INDEX UX_TravelPackages_Tenant_Code ON dbo.TravelPackages (TenantId, Code) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelPackageHotels', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelPackageHotels (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelPackageHotels PRIMARY KEY,
                    TenantId INT NOT NULL,
                    PackageId INT NOT NULL,
                    HotelId INT NOT NULL,
                    Nights INT NOT NULL,
                    Sequence INT NOT NULL,
                    CONSTRAINT FK_TravelPackageHotels_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelPackageHotels_Package FOREIGN KEY (PackageId) REFERENCES dbo.TravelPackages(Id),
                    CONSTRAINT FK_TravelPackageHotels_Hotel FOREIGN KEY (HotelId) REFERENCES dbo.TravelHotels(Id),
                    CONSTRAINT CK_TravelPackageHotels_Nights CHECK (Nights BETWEEN 1 AND 90)
                );
                CREATE INDEX IX_TravelPackageHotels_Tenant_Package ON dbo.TravelPackageHotels (TenantId, PackageId);
            END

            IF OBJECT_ID(N'dbo.TravelPackagePrices', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelPackagePrices (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelPackagePrices PRIMARY KEY,
                    TenantId INT NOT NULL,
                    PackageId INT NOT NULL,
                    RoomType NVARCHAR(10) NOT NULL,
                    AdultPrice DECIMAL(18,2) NOT NULL,
                    ChildPrice DECIMAL(18,2) NOT NULL,
                    InfantPrice DECIMAL(18,2) NOT NULL,
                    CONSTRAINT FK_TravelPackagePrices_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelPackagePrices_Package FOREIGN KEY (PackageId) REFERENCES dbo.TravelPackages(Id),
                    CONSTRAINT CK_TravelPackagePrices_Room CHECK (RoomType IN (N'Quad', N'Triple', N'Double', N'Single')),
                    CONSTRAINT CK_TravelPackagePrices_Positive CHECK (AdultPrice >= 0 AND ChildPrice >= 0 AND InfantPrice >= 0)
                );
                CREATE UNIQUE INDEX UX_TravelPackagePrices_Package_Room ON dbo.TravelPackagePrices (TenantId, PackageId, RoomType);
            END
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelGroups', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelGroups (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelGroups PRIMARY KEY,
                    TenantId INT NOT NULL,
                    GroupNumber NVARCHAR(30) NOT NULL,
                    PackageId INT NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    DepartureDate DATE NOT NULL,
                    ReturnDate DATE NOT NULL,
                    Capacity INT NOT NULL,
                    GroupLeaderTravelerId INT NULL,
                    LeaderName NVARCHAR(100) NULL,
                    LeaderPhone NVARCHAR(30) NULL,
                    VisaCaseId INT NULL,
                    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_TravelGroups_Status DEFAULT (N'Draft'),
                    ClosedAt DATETIME2 NULL,
                    CancelReason NVARCHAR(500) NULL,
                    BranchId INT NULL,
                    Notes NVARCHAR(2000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelGroups_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelGroups_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelGroups_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelGroups_Package FOREIGN KEY (PackageId) REFERENCES dbo.TravelPackages(Id),
                    CONSTRAINT FK_TravelGroups_Leader FOREIGN KEY (GroupLeaderTravelerId) REFERENCES dbo.TravelTravelers(Id),
                    CONSTRAINT FK_TravelGroups_VisaCase FOREIGN KEY (VisaCaseId) REFERENCES dbo.TravelVisaCases(Id),
                    CONSTRAINT FK_TravelGroups_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id),
                    CONSTRAINT CK_TravelGroups_Status CHECK (Status IN (N'Draft', N'Open', N'RegistrationClosed', N'VisaProcessing',
                        N'Ready', N'Departed', N'Returned', N'Closed', N'Cancelled')),
                    CONSTRAINT CK_TravelGroups_Dates CHECK (ReturnDate >= DepartureDate),
                    CONSTRAINT CK_TravelGroups_Capacity CHECK (Capacity BETWEEN 1 AND 1000),
                    CONSTRAINT CK_TravelGroups_Cancel CHECK (Status <> N'Cancelled' OR CancelReason IS NOT NULL),
                    CONSTRAINT CK_TravelGroups_Visa CHECK (Status IN (N'Draft', N'Open', N'RegistrationClosed', N'Cancelled') OR VisaCaseId IS NOT NULL)
                );
                CREATE UNIQUE INDEX UX_TravelGroups_Tenant_Number ON dbo.TravelGroups (TenantId, GroupNumber);
                CREATE INDEX IX_TravelGroups_Tenant_Status ON dbo.TravelGroups (TenantId, Status) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelGroups_Tenant_Departure ON dbo.TravelGroups (TenantId, DepartureDate) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelGroupMembers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelGroupMembers (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelGroupMembers PRIMARY KEY,
                    TenantId INT NOT NULL,
                    GroupId INT NOT NULL,
                    TravelerId INT NOT NULL,
                    CustomerId INT NOT NULL,
                    RoomPreference NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelGroupMembers_Room DEFAULT (N'Quad'),
                    PriceCategory NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelGroupMembers_Category DEFAULT (N'Adult'),
                    QuotedPrice DECIMAL(18,2) NULL,
                    CurrencyCode CHAR(3) NULL,
                    Status NVARCHAR(12) NOT NULL CONSTRAINT DF_TravelGroupMembers_Status DEFAULT (N'Registered'),
                    CancelReason NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelGroupMembers_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelGroupMembers_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelGroupMembers_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelGroupMembers_Group FOREIGN KEY (GroupId) REFERENCES dbo.TravelGroups(Id),
                    CONSTRAINT FK_TravelGroupMembers_Traveler FOREIGN KEY (TravelerId) REFERENCES dbo.TravelTravelers(Id),
                    CONSTRAINT FK_TravelGroupMembers_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
                    CONSTRAINT CK_TravelGroupMembers_Room CHECK (RoomPreference IN (N'Quad', N'Triple', N'Double', N'Single')),
                    CONSTRAINT CK_TravelGroupMembers_Category CHECK (PriceCategory IN (N'Adult', N'Child', N'Infant')),
                    CONSTRAINT CK_TravelGroupMembers_Status CHECK (Status IN (N'Registered', N'Confirmed', N'Cancelled')),
                    CONSTRAINT CK_TravelGroupMembers_Price CHECK (QuotedPrice IS NULL OR (QuotedPrice >= 0 AND CurrencyCode IS NOT NULL)),
                    CONSTRAINT CK_TravelGroupMembers_Cancel CHECK (Status <> N'Cancelled' OR CancelReason IS NOT NULL)
                );
                CREATE UNIQUE INDEX UX_TravelGroupMembers_Group_Traveler
                    ON dbo.TravelGroupMembers (TenantId, GroupId, TravelerId) WHERE IsDeleted = 0 AND Status <> N'Cancelled';
                CREATE INDEX IX_TravelGroupMembers_Tenant_Traveler ON dbo.TravelGroupMembers (TenantId, TravelerId) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelGroupMembers_Tenant_Customer ON dbo.TravelGroupMembers (TenantId, CustomerId) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelGroupHotels', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelGroupHotels (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelGroupHotels PRIMARY KEY,
                    TenantId INT NOT NULL,
                    GroupId INT NOT NULL,
                    HotelId INT NOT NULL,
                    CheckIn DATE NOT NULL,
                    CheckOut DATE NOT NULL,
                    Sequence INT NOT NULL,
                    ConfirmationNumber NVARCHAR(60) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelGroupHotels_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelGroupHotels_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelGroupHotels_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelGroupHotels_Group FOREIGN KEY (GroupId) REFERENCES dbo.TravelGroups(Id),
                    CONSTRAINT FK_TravelGroupHotels_Hotel FOREIGN KEY (HotelId) REFERENCES dbo.TravelHotels(Id),
                    CONSTRAINT CK_TravelGroupHotels_Dates CHECK (CheckOut > CheckIn)
                );
                CREATE INDEX IX_TravelGroupHotels_Tenant_Group ON dbo.TravelGroupHotels (TenantId, GroupId) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelGroupRooms', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelGroupRooms (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelGroupRooms PRIMARY KEY,
                    TenantId INT NOT NULL,
                    GroupHotelId INT NOT NULL,
                    RoomNumber NVARCHAR(20) NULL,
                    RoomType NVARCHAR(10) NOT NULL,
                    Capacity INT NOT NULL,
                    GenderPolicy NVARCHAR(10) NOT NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelGroupRooms_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelGroupRooms_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelGroupRooms_Stay FOREIGN KEY (GroupHotelId) REFERENCES dbo.TravelGroupHotels(Id),
                    CONSTRAINT CK_TravelGroupRooms_Type CHECK (RoomType IN (N'Quad', N'Triple', N'Double', N'Single')),
                    CONSTRAINT CK_TravelGroupRooms_Capacity CHECK (Capacity BETWEEN 1 AND 6),
                    CONSTRAINT CK_TravelGroupRooms_Gender CHECK (GenderPolicy IN (N'Male', N'Female', N'Family'))
                );
                CREATE INDEX IX_TravelGroupRooms_Tenant_Stay ON dbo.TravelGroupRooms (TenantId, GroupHotelId) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelRoomAssignments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelRoomAssignments (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelRoomAssignments PRIMARY KEY,
                    TenantId INT NOT NULL,
                    GroupHotelId INT NOT NULL,
                    RoomId INT NOT NULL,
                    MemberId INT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelRoomAssignments_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    CONSTRAINT FK_TravelRoomAssignments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelRoomAssignments_Stay FOREIGN KEY (GroupHotelId) REFERENCES dbo.TravelGroupHotels(Id),
                    CONSTRAINT FK_TravelRoomAssignments_Room FOREIGN KEY (RoomId) REFERENCES dbo.TravelGroupRooms(Id),
                    CONSTRAINT FK_TravelRoomAssignments_Member FOREIGN KEY (MemberId) REFERENCES dbo.TravelGroupMembers(Id)
                );
                -- One bed per pilgrim per stay.
                CREATE UNIQUE INDEX UX_TravelRoomAssignments_Stay_Member ON dbo.TravelRoomAssignments (TenantId, GroupHotelId, MemberId);
                CREATE INDEX IX_TravelRoomAssignments_Tenant_Room ON dbo.TravelRoomAssignments (TenantId, RoomId);
            END

            IF OBJECT_ID(N'dbo.TravelGroupTransport', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelGroupTransport (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelGroupTransport PRIMARY KEY,
                    TenantId INT NOT NULL,
                    GroupId INT NOT NULL,
                    LegType NVARCHAR(20) NOT NULL,
                    DepartAt DATETIME2 NOT NULL,
                    FromPlace NVARCHAR(150) NOT NULL,
                    ToPlace NVARCHAR(150) NOT NULL,
                    ProviderName NVARCHAR(150) NULL,
                    VehicleType NVARCHAR(60) NULL,
                    Seats INT NULL,
                    FleetBookingId INT NULL,
                    Notes NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelGroupTransport_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelGroupTransport_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelGroupTransport_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelGroupTransport_Group FOREIGN KEY (GroupId) REFERENCES dbo.TravelGroups(Id),
                    CONSTRAINT FK_TravelGroupTransport_FleetBooking FOREIGN KEY (FleetBookingId) REFERENCES dbo.Bookings(Id),
                    CONSTRAINT CK_TravelGroupTransport_Type CHECK (LegType IN (N'AirportTransfer', N'IntercityTransfer', N'Ziyarat', N'Other')),
                    CONSTRAINT CK_TravelGroupTransport_Seats CHECK (Seats IS NULL OR Seats BETWEEN 1 AND 1000)
                );
                CREATE INDEX IX_TravelGroupTransport_Tenant_Group ON dbo.TravelGroupTransport (TenantId, GroupId, DepartAt) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelGroupStatusHistory', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelGroupStatusHistory (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelGroupStatusHistory PRIMARY KEY,
                    TenantId INT NOT NULL,
                    GroupId INT NOT NULL,
                    MemberId INT NULL,
                    EventType NVARCHAR(20) NOT NULL,
                    FromValue NVARCHAR(30) NULL,
                    ToValue NVARCHAR(30) NULL,
                    Note NVARCHAR(1000) NULL,
                    ChangedBy INT NULL,
                    ChangedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelGroupStatusHistory_At DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_TravelGroupStatusHistory_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelGroupStatusHistory_Group FOREIGN KEY (GroupId) REFERENCES dbo.TravelGroups(Id),
                    CONSTRAINT FK_TravelGroupStatusHistory_Member FOREIGN KEY (MemberId) REFERENCES dbo.TravelGroupMembers(Id),
                    CONSTRAINT CK_TravelGroupStatusHistory_Event CHECK (EventType IN (N'GroupStatus', N'MemberRegistered', N'MemberConfirmed',
                        N'MemberCancelled', N'RoomAssigned', N'RoomUnassigned', N'LeaderChanged', N'ManifestGenerated'))
                );
                CREATE INDEX IX_TravelGroupStatusHistory_Tenant_Group ON dbo.TravelGroupStatusHistory (TenantId, GroupId, ChangedAt);
            END
            """, cancellationToken: cancellationToken));

        foreach (var (code, desc) in PermissionRows)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
                AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (N'Travel', @Code, @Desc);
                """, new { Code = code, Desc = desc }, cancellationToken: cancellationToken));
        }

        foreach (var role in new[] { "SUPER_ADMIN", "TENANT_ADMIN", "OWNER" })
        {
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection, role, TravelPermissions.Phase4, cancellationToken);
        }

        var hasMenuTables = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(N'dbo.PlatformMenus', N'U') IS NOT NULL
                         AND OBJECT_ID(N'dbo.PlatformModules', N'U') IS NOT NULL THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken)) == 1;
        if (hasMenuTables)
        {
            var menus = new (string Name, string Route, string Icon, string Perm, int Sort)[]
            {
                ("Umrah Groups", "/travel/groups", "groups_3", TravelPermissions.GroupView, 60),
                ("Packages", "/travel/packages", "card_travel", TravelPermissions.PackageView, 70),
            };
            foreach (var m in menus)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE pm SET
                        pm.ModuleId = mod.Id, pm.ParentId = NULL, pm.Name = @Name, pm.Icon = @Icon,
                        pm.PermissionCode = @Perm, pm.SortOrder = @Sort, pm.IsActive = 1, pm.Visible = 1,
                        pm.DisplayName = @Name, pm.Category = N'Travel', pm.ModuleKey = @ModuleCode,
                        pm.FeatureKey = @FeatureKey, pm.UpdatedAt = SYSUTCDATETIME()
                    FROM PlatformMenus pm
                    CROSS JOIN PlatformModules mod
                    WHERE mod.ModuleKey = @PlatformModule AND pm.Route = @Route;

                    IF @@ROWCOUNT = 0
                    INSERT INTO PlatformMenus (
                        ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                        DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
                    SELECT mod.Id, NULL, @Name, @Route, @Icon, @Perm, @Sort, 1,
                           @Name, NULL, N'Travel', 1, @FeatureKey, @ModuleCode, 0, SYSUTCDATETIME()
                    FROM PlatformModules mod
                    WHERE mod.ModuleKey = @PlatformModule
                      AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.Route = @Route);
                    """,
                    new
                    {
                        PlatformModule = "travel-nexus", m.Name, m.Route, m.Icon, m.Perm, m.Sort,
                        ModuleCode = TenantModuleCatalog.TravelAgencyModuleCode,
                        FeatureKey = TenantModuleCatalog.TravelAgencyLegacyKey
                    }, cancellationToken: cancellationToken));
            }
        }

        logger.LogInformation("TravelNexusUmrahMigration applied (hotels, packages, groups, pilgrims, rooms, transport, permissions).");
    }
}
