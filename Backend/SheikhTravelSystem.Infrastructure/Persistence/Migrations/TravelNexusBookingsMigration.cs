using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// TravelNexus Phase 5: airports (platform reference data), suppliers, booking files, flight bookings (segments,
/// passengers, tickets), hotel bookings (rooms, guests), cancellations with refund tracking and booking history;
/// booking number prefix; Phase 5 permissions and menu rows. Additive and idempotent.
/// </summary>
public static class TravelNexusBookingsMigration
{
    private static readonly (string Code, string Desc)[] PermissionRows =
    [
        (TravelPermissions.SupplierView, "View travel suppliers"),
        (TravelPermissions.SupplierManage, "Manage travel suppliers (airlines, hotels, bed banks, consolidators, transport)"),
        (TravelPermissions.BookingView, "View booking files, flights and hotel bookings"),
        (TravelPermissions.BookingManage, "Manage booking files, flights, passengers, tickets and hotel bookings"),
        (TravelPermissions.BookingCancel, "Cancel bookings and track penalties and refunds"),
    ];

    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // ── Reference data and suppliers ─────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'dbo.TravelSettings', N'BookingPrefix') IS NULL
                ALTER TABLE dbo.TravelSettings ADD BookingPrefix NVARCHAR(10) NOT NULL
                    CONSTRAINT DF_TravelSettings_BookingPrefix DEFAULT (N'BKG');

            IF OBJECT_ID(N'dbo.TravelAirports', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelAirports (
                    IataCode CHAR(3) NOT NULL CONSTRAINT PK_TravelAirports PRIMARY KEY,
                    Name NVARCHAR(150) NOT NULL,
                    City NVARCHAR(100) NOT NULL,
                    CountryCode CHAR(2) NOT NULL,
                    IanaTimeZone NVARCHAR(64) NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelAirports_IsActive DEFAULT (1),
                    CONSTRAINT FK_TravelAirports_Country FOREIGN KEY (CountryCode) REFERENCES dbo.TravelCountries(Code)
                );
            END

            IF OBJECT_ID(N'dbo.TravelSuppliers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSuppliers (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelSuppliers PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    SupplierType NVARCHAR(20) NOT NULL,
                    IataCode NVARCHAR(3) NULL,
                    ContactName NVARCHAR(150) NULL,
                    Phone NVARCHAR(30) NULL,
                    Email NVARCHAR(200) NULL,
                    CurrencyCode CHAR(3) NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelSuppliers_IsActive DEFAULT (1),
                    Notes NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelSuppliers_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelSuppliers_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelSuppliers_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT CK_TravelSuppliers_Type CHECK (SupplierType IN (N'Airline', N'Hotel', N'BedBank', N'Consolidator', N'Transport', N'Other'))
                );
                CREATE UNIQUE INDEX UX_TravelSuppliers_Tenant_Name ON dbo.TravelSuppliers (TenantId, Name) WHERE IsDeleted = 0;
            END
            """, cancellationToken: cancellationToken));

        foreach (var a in TravelAirportCatalog.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM dbo.TravelAirports WHERE IataCode = @Code)
                    INSERT INTO dbo.TravelAirports (IataCode, Name, City, CountryCode, IanaTimeZone)
                    VALUES (@Code, @Name, @City, @CountryCode, @TimeZone);
                """, new { a.Code, a.Name, a.City, a.CountryCode, a.TimeZone }, cancellationToken: cancellationToken));
        }

        // ── Booking files ────────────────────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelBookings', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelBookings (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelBookings PRIMARY KEY,
                    TenantId INT NOT NULL,
                    BookingNumber NVARCHAR(30) NOT NULL,
                    CustomerId INT NULL,
                    GroupId INT NULL,
                    EnquiryId INT NULL,
                    VisaCaseId INT NULL,
                    Title NVARCHAR(150) NOT NULL,
                    Status NVARCHAR(12) NOT NULL CONSTRAINT DF_TravelBookings_Status DEFAULT (N'Open'),
                    AssignedAgentUserId INT NULL,
                    BranchId INT NULL,
                    Notes NVARCHAR(2000) NULL,
                    CompletedAt DATETIME2 NULL,
                    CancelReason NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelBookings_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelBookings_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelBookings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelBookings_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
                    CONSTRAINT FK_TravelBookings_Group FOREIGN KEY (GroupId) REFERENCES dbo.TravelGroups(Id),
                    CONSTRAINT FK_TravelBookings_Enquiry FOREIGN KEY (EnquiryId) REFERENCES dbo.TravelEnquiries(Id),
                    CONSTRAINT FK_TravelBookings_VisaCase FOREIGN KEY (VisaCaseId) REFERENCES dbo.TravelVisaCases(Id),
                    CONSTRAINT FK_TravelBookings_Agent FOREIGN KEY (AssignedAgentUserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT FK_TravelBookings_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id),
                    CONSTRAINT CK_TravelBookings_Owner CHECK ((CustomerId IS NULL AND GroupId IS NOT NULL) OR (CustomerId IS NOT NULL AND GroupId IS NULL)),
                    CONSTRAINT CK_TravelBookings_Status CHECK (Status IN (N'Open', N'Completed', N'Cancelled')),
                    CONSTRAINT CK_TravelBookings_CancelReason CHECK (Status <> N'Cancelled' OR CancelReason IS NOT NULL),
                    CONSTRAINT CK_TravelBookings_Completed CHECK (Status <> N'Completed' OR CompletedAt IS NOT NULL)
                );
                CREATE UNIQUE INDEX UX_TravelBookings_Tenant_Number ON dbo.TravelBookings (TenantId, BookingNumber);
                CREATE INDEX IX_TravelBookings_Tenant_Customer ON dbo.TravelBookings (TenantId, CustomerId) WHERE CustomerId IS NOT NULL;
                CREATE INDEX IX_TravelBookings_Tenant_Group ON dbo.TravelBookings (TenantId, GroupId) WHERE GroupId IS NOT NULL;
                CREATE INDEX IX_TravelBookings_Tenant_Status ON dbo.TravelBookings (TenantId, Status) INCLUDE (CreatedAt);
            END
            """, cancellationToken: cancellationToken));

        // ── Flights ──────────────────────────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelFlightBookings', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelFlightBookings (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelFlightBookings PRIMARY KEY,
                    TenantId INT NOT NULL,
                    BookingId INT NOT NULL,
                    SupplierId INT NULL,
                    Direction NVARCHAR(12) NOT NULL,
                    Pnr NVARCHAR(20) NULL,
                    AirlinePnr NVARCHAR(20) NULL,
                    Status NVARCHAR(12) NOT NULL CONSTRAINT DF_TravelFlightBookings_Status DEFAULT (N'Requested'),
                    TicketingTimeLimitUtc DATETIME2 NULL,
                    SellAmount DECIMAL(18,2) NULL,
                    CostAmount DECIMAL(18,2) NULL,
                    CurrencyCode CHAR(3) NULL,
                    Notes NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelFlightBookings_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelFlightBookings_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelFlightBookings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelFlightBookings_Booking FOREIGN KEY (BookingId) REFERENCES dbo.TravelBookings(Id),
                    CONSTRAINT FK_TravelFlightBookings_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.TravelSuppliers(Id),
                    CONSTRAINT CK_TravelFlightBookings_Direction CHECK (Direction IN (N'Outbound', N'Return', N'RoundTrip', N'OneWay', N'Internal')),
                    CONSTRAINT CK_TravelFlightBookings_Status CHECK (Status IN (N'Requested', N'OnHold', N'Confirmed', N'Ticketed', N'Cancelled')),
                    CONSTRAINT CK_TravelFlightBookings_OnHold CHECK (Status <> N'OnHold' OR (Pnr IS NOT NULL AND TicketingTimeLimitUtc IS NOT NULL)),
                    CONSTRAINT CK_TravelFlightBookings_Pnr CHECK (Status NOT IN (N'Confirmed', N'Ticketed') OR Pnr IS NOT NULL),
                    CONSTRAINT CK_TravelFlightBookings_Amounts CHECK ((SellAmount IS NULL OR SellAmount >= 0) AND (CostAmount IS NULL OR CostAmount >= 0)
                        AND ((SellAmount IS NULL AND CostAmount IS NULL) OR CurrencyCode IS NOT NULL))
                );
                CREATE INDEX IX_TravelFlightBookings_Tenant_Booking ON dbo.TravelFlightBookings (TenantId, BookingId);
                CREATE UNIQUE INDEX UX_TravelFlightBookings_Tenant_Pnr ON dbo.TravelFlightBookings (TenantId, Pnr)
                    WHERE Pnr IS NOT NULL AND Status <> N'Cancelled' AND IsDeleted = 0;
                CREATE INDEX IX_TravelFlightBookings_Tenant_TimeLimit ON dbo.TravelFlightBookings (TenantId, TicketingTimeLimitUtc)
                    WHERE Status = N'OnHold' AND IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelFlightSegments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelFlightSegments (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelFlightSegments PRIMARY KEY,
                    TenantId INT NOT NULL,
                    FlightBookingId INT NOT NULL,
                    Sequence INT NOT NULL,
                    AirlineCode NVARCHAR(3) NOT NULL,
                    FlightNumber NVARCHAR(6) NOT NULL,
                    FromAirport CHAR(3) NOT NULL,
                    ToAirport CHAR(3) NOT NULL,
                    DepartLocal DATETIME2 NOT NULL,
                    ArriveLocal DATETIME2 NOT NULL,
                    DepartUtc DATETIME2 NOT NULL,
                    ArriveUtc DATETIME2 NOT NULL,
                    CabinClass NVARCHAR(16) NOT NULL CONSTRAINT DF_TravelFlightSegments_Cabin DEFAULT (N'Economy'),
                    BookingClass NVARCHAR(2) NULL,
                    Baggage NVARCHAR(30) NULL,
                    CONSTRAINT FK_TravelFlightSegments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelFlightSegments_Flight FOREIGN KEY (FlightBookingId) REFERENCES dbo.TravelFlightBookings(Id),
                    CONSTRAINT FK_TravelFlightSegments_From FOREIGN KEY (FromAirport) REFERENCES dbo.TravelAirports(IataCode),
                    CONSTRAINT FK_TravelFlightSegments_To FOREIGN KEY (ToAirport) REFERENCES dbo.TravelAirports(IataCode),
                    CONSTRAINT CK_TravelFlightSegments_Route CHECK (FromAirport <> ToAirport),
                    CONSTRAINT CK_TravelFlightSegments_Times CHECK (ArriveUtc >= DepartUtc),
                    CONSTRAINT CK_TravelFlightSegments_Cabin CHECK (CabinClass IN (N'Economy', N'PremiumEconomy', N'Business', N'First'))
                );
                CREATE INDEX IX_TravelFlightSegments_Tenant_Flight ON dbo.TravelFlightSegments (TenantId, FlightBookingId);
            END

            IF OBJECT_ID(N'dbo.TravelFlightPassengers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelFlightPassengers (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelFlightPassengers PRIMARY KEY,
                    TenantId INT NOT NULL,
                    FlightBookingId INT NOT NULL,
                    TravelerId INT NOT NULL,
                    GroupMemberId INT NULL,
                    PassengerType NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelFlightPassengers_Type DEFAULT (N'Adult'),
                    TicketNumber NVARCHAR(20) NULL,
                    TicketStatus NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelFlightPassengers_Ticket DEFAULT (N'NotIssued'),
                    CONSTRAINT FK_TravelFlightPassengers_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelFlightPassengers_Flight FOREIGN KEY (FlightBookingId) REFERENCES dbo.TravelFlightBookings(Id),
                    CONSTRAINT FK_TravelFlightPassengers_Traveler FOREIGN KEY (TravelerId) REFERENCES dbo.TravelTravelers(Id),
                    CONSTRAINT FK_TravelFlightPassengers_Member FOREIGN KEY (GroupMemberId) REFERENCES dbo.TravelGroupMembers(Id),
                    CONSTRAINT CK_TravelFlightPassengers_Type CHECK (PassengerType IN (N'Adult', N'Child', N'Infant')),
                    CONSTRAINT CK_TravelFlightPassengers_Ticket CHECK (TicketStatus IN (N'NotIssued', N'Issued', N'Voided', N'Refunded')),
                    CONSTRAINT CK_TravelFlightPassengers_Number CHECK (TicketStatus = N'NotIssued' OR TicketNumber IS NOT NULL)
                );
                CREATE UNIQUE INDEX UX_TravelFlightPassengers_Flight_Traveler ON dbo.TravelFlightPassengers (TenantId, FlightBookingId, TravelerId);
                CREATE UNIQUE INDEX UX_TravelFlightPassengers_Tenant_Ticket ON dbo.TravelFlightPassengers (TenantId, TicketNumber) WHERE TicketNumber IS NOT NULL;
                CREATE INDEX IX_TravelFlightPassengers_Tenant_Member ON dbo.TravelFlightPassengers (TenantId, GroupMemberId) WHERE GroupMemberId IS NOT NULL;
            END
            """, cancellationToken: cancellationToken));

        // ── Hotels ───────────────────────────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelHotelBookings', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelHotelBookings (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelHotelBookings PRIMARY KEY,
                    TenantId INT NOT NULL,
                    BookingId INT NOT NULL,
                    HotelId INT NOT NULL,
                    SupplierId INT NULL,
                    GroupHotelId INT NULL,
                    CheckIn DATE NOT NULL,
                    CheckOut DATE NOT NULL,
                    MealPlan NVARCHAR(12) NOT NULL CONSTRAINT DF_TravelHotelBookings_Meal DEFAULT (N'RoomOnly'),
                    ConfirmationNumber NVARCHAR(50) NULL,
                    Status NVARCHAR(12) NOT NULL CONSTRAINT DF_TravelHotelBookings_Status DEFAULT (N'Requested'),
                    SpecialRequests NVARCHAR(1000) NULL,
                    SellAmount DECIMAL(18,2) NULL,
                    CostAmount DECIMAL(18,2) NULL,
                    CurrencyCode CHAR(3) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelHotelBookings_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelHotelBookings_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelHotelBookings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelHotelBookings_Booking FOREIGN KEY (BookingId) REFERENCES dbo.TravelBookings(Id),
                    CONSTRAINT FK_TravelHotelBookings_Hotel FOREIGN KEY (HotelId) REFERENCES dbo.TravelHotels(Id),
                    CONSTRAINT FK_TravelHotelBookings_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.TravelSuppliers(Id),
                    CONSTRAINT FK_TravelHotelBookings_GroupHotel FOREIGN KEY (GroupHotelId) REFERENCES dbo.TravelGroupHotels(Id),
                    CONSTRAINT CK_TravelHotelBookings_Dates CHECK (CheckOut > CheckIn),
                    CONSTRAINT CK_TravelHotelBookings_Meal CHECK (MealPlan IN (N'RoomOnly', N'Breakfast', N'HalfBoard', N'FullBoard')),
                    CONSTRAINT CK_TravelHotelBookings_Status CHECK (Status IN (N'Requested', N'Confirmed', N'CheckedIn', N'CheckedOut', N'NoShow', N'Cancelled')),
                    CONSTRAINT CK_TravelHotelBookings_Confirmation CHECK (Status IN (N'Requested', N'Cancelled') OR ConfirmationNumber IS NOT NULL),
                    CONSTRAINT CK_TravelHotelBookings_Amounts CHECK ((SellAmount IS NULL OR SellAmount >= 0) AND (CostAmount IS NULL OR CostAmount >= 0)
                        AND ((SellAmount IS NULL AND CostAmount IS NULL) OR CurrencyCode IS NOT NULL))
                );
                CREATE INDEX IX_TravelHotelBookings_Tenant_Booking ON dbo.TravelHotelBookings (TenantId, BookingId);
                CREATE INDEX IX_TravelHotelBookings_Tenant_GroupHotel ON dbo.TravelHotelBookings (TenantId, GroupHotelId) WHERE GroupHotelId IS NOT NULL;
            END

            IF OBJECT_ID(N'dbo.TravelHotelBookingRooms', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelHotelBookingRooms (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelHotelBookingRooms PRIMARY KEY,
                    TenantId INT NOT NULL,
                    HotelBookingId INT NOT NULL,
                    RoomType NVARCHAR(10) NOT NULL,
                    Quantity INT NOT NULL,
                    CONSTRAINT FK_TravelHotelBookingRooms_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelHotelBookingRooms_Hotel FOREIGN KEY (HotelBookingId) REFERENCES dbo.TravelHotelBookings(Id),
                    CONSTRAINT CK_TravelHotelBookingRooms_Type CHECK (RoomType IN (N'Quad', N'Triple', N'Double', N'Single')),
                    CONSTRAINT CK_TravelHotelBookingRooms_Qty CHECK (Quantity BETWEEN 1 AND 500)
                );
                CREATE UNIQUE INDEX UX_TravelHotelBookingRooms_Booking_Type ON dbo.TravelHotelBookingRooms (TenantId, HotelBookingId, RoomType);
            END

            IF OBJECT_ID(N'dbo.TravelHotelBookingGuests', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelHotelBookingGuests (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelHotelBookingGuests PRIMARY KEY,
                    TenantId INT NOT NULL,
                    HotelBookingId INT NOT NULL,
                    TravelerId INT NOT NULL,
                    CONSTRAINT FK_TravelHotelBookingGuests_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelHotelBookingGuests_Hotel FOREIGN KEY (HotelBookingId) REFERENCES dbo.TravelHotelBookings(Id),
                    CONSTRAINT FK_TravelHotelBookingGuests_Traveler FOREIGN KEY (TravelerId) REFERENCES dbo.TravelTravelers(Id)
                );
                CREATE UNIQUE INDEX UX_TravelHotelBookingGuests_Booking_Traveler ON dbo.TravelHotelBookingGuests (TenantId, HotelBookingId, TravelerId);
            END
            """, cancellationToken: cancellationToken));

        // ── Cancellations and history ────────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelBookingCancellations', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelBookingCancellations (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelBookingCancellations PRIMARY KEY,
                    TenantId INT NOT NULL,
                    BookingId INT NOT NULL,
                    ServiceType NVARCHAR(10) NOT NULL,
                    FlightBookingId INT NULL,
                    HotelBookingId INT NULL,
                    FlightPassengerId INT NULL,
                    Reason NVARCHAR(500) NOT NULL,
                    SupplierPenalty DECIMAL(18,2) NOT NULL CONSTRAINT DF_TravelBookingCancellations_SP DEFAULT (0),
                    CustomerPenalty DECIMAL(18,2) NOT NULL CONSTRAINT DF_TravelBookingCancellations_CP DEFAULT (0),
                    RefundFromSupplier DECIMAL(18,2) NOT NULL CONSTRAINT DF_TravelBookingCancellations_RS DEFAULT (0),
                    RefundToCustomer DECIMAL(18,2) NOT NULL CONSTRAINT DF_TravelBookingCancellations_RC DEFAULT (0),
                    CurrencyCode CHAR(3) NULL,
                    RefundStatus NVARCHAR(24) NOT NULL CONSTRAINT DF_TravelBookingCancellations_Refund DEFAULT (N'NotApplicable'),
                    CancelledAt DATETIME2 NOT NULL CONSTRAINT DF_TravelBookingCancellations_At DEFAULT (SYSUTCDATETIME()),
                    CancelledBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    CONSTRAINT FK_TravelBookingCancellations_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelBookingCancellations_Booking FOREIGN KEY (BookingId) REFERENCES dbo.TravelBookings(Id),
                    CONSTRAINT FK_TravelBookingCancellations_Flight FOREIGN KEY (FlightBookingId) REFERENCES dbo.TravelFlightBookings(Id),
                    CONSTRAINT FK_TravelBookingCancellations_Hotel FOREIGN KEY (HotelBookingId) REFERENCES dbo.TravelHotelBookings(Id),
                    CONSTRAINT FK_TravelBookingCancellations_Passenger FOREIGN KEY (FlightPassengerId) REFERENCES dbo.TravelFlightPassengers(Id),
                    CONSTRAINT CK_TravelBookingCancellations_Service CHECK (
                        (ServiceType = N'Flight' AND FlightBookingId IS NOT NULL AND HotelBookingId IS NULL)
                        OR (ServiceType = N'Hotel' AND HotelBookingId IS NOT NULL AND FlightBookingId IS NULL AND FlightPassengerId IS NULL)),
                    CONSTRAINT CK_TravelBookingCancellations_Amounts CHECK (SupplierPenalty >= 0 AND CustomerPenalty >= 0
                        AND RefundFromSupplier >= 0 AND RefundToCustomer >= 0),
                    CONSTRAINT CK_TravelBookingCancellations_Currency CHECK (CurrencyCode IS NOT NULL
                        OR (SupplierPenalty = 0 AND CustomerPenalty = 0 AND RefundFromSupplier = 0 AND RefundToCustomer = 0)),
                    CONSTRAINT CK_TravelBookingCancellations_Refund CHECK (RefundStatus IN
                        (N'NotApplicable', N'Pending', N'ReceivedFromSupplier', N'PaidToCustomer', N'Closed'))
                );
                CREATE INDEX IX_TravelBookingCancellations_Tenant_Booking ON dbo.TravelBookingCancellations (TenantId, BookingId);
            END

            IF OBJECT_ID(N'dbo.TravelBookingHistory', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelBookingHistory (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelBookingHistory PRIMARY KEY,
                    TenantId INT NOT NULL,
                    BookingId INT NOT NULL,
                    ServiceType NVARCHAR(10) NULL,
                    ServiceId INT NULL,
                    EventType NVARCHAR(20) NOT NULL,
                    FromValue NVARCHAR(100) NULL,
                    ToValue NVARCHAR(100) NULL,
                    Note NVARCHAR(1000) NULL,
                    ChangedBy INT NULL,
                    ChangedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelBookingHistory_At DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_TravelBookingHistory_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelBookingHistory_Booking FOREIGN KEY (BookingId) REFERENCES dbo.TravelBookings(Id),
                    CONSTRAINT CK_TravelBookingHistory_Event CHECK (EventType IN (N'FileStatus', N'FlightStatus', N'HotelStatus', N'TicketIssued',
                        N'TicketVoided', N'Cancelled', N'RefundStatus', N'ServiceAdded', N'ServiceUpdated'))
                );
                CREATE INDEX IX_TravelBookingHistory_Tenant_Booking ON dbo.TravelBookingHistory (TenantId, BookingId, ChangedAt);
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
                connection, role, TravelPermissions.Phase5, cancellationToken);
        }

        var hasMenuTables = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(N'dbo.PlatformMenus', N'U') IS NOT NULL
                         AND OBJECT_ID(N'dbo.PlatformModules', N'U') IS NOT NULL THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken)) == 1;
        if (hasMenuTables)
        {
            var menus = new (string Name, string Route, string Icon, string Perm, int Sort)[]
            {
                ("Bookings", "/travel/bookings", "airplane_ticket", TravelPermissions.BookingView, 80),
                ("Suppliers", "/travel/suppliers", "storefront", TravelPermissions.SupplierView, 90),
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

        logger.LogInformation("TravelNexusBookingsMigration applied (airports, suppliers, bookings, flights, hotels, cancellations, permissions).");
    }
}
