using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Geofences workspace: groups, custom types, address, per-zone GPS buffer, corridor width, stored area/perimeter,
/// "applies to all vehicles", the rule columns (dwell, speed inside, idle, not arrived by, engine block on exit),
/// an active schedule, notify channels and cooldown; per-geofence recipients; driver-scoped assignments; one row per
/// visit (enter → exit) in <c>GeofenceVisits</c>; and a once-per-day log for "no vehicle arrived by" alerts.
/// Additive and idempotent. Statements that reference a column added here run in a later batch.
/// </summary>
public static class GeofenceWorkspaceMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // ── Geofence columns ─────────────────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'dbo.Geofences', N'GroupId') IS NULL
                ALTER TABLE dbo.Geofences ADD GroupId INT NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'Address') IS NULL
                ALTER TABLE dbo.Geofences ADD Address NVARCHAR(300) NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'BufferMeters') IS NULL
                ALTER TABLE dbo.Geofences ADD BufferMeters INT NOT NULL CONSTRAINT DF_Geofences_BufferMeters DEFAULT (25);
            IF COL_LENGTH(N'dbo.Geofences', N'CorridorWidthMeters') IS NULL
                ALTER TABLE dbo.Geofences ADD CorridorWidthMeters INT NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'AreaSquareMeters') IS NULL
                ALTER TABLE dbo.Geofences ADD AreaSquareMeters FLOAT NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'PerimeterMeters') IS NULL
                ALTER TABLE dbo.Geofences ADD PerimeterMeters FLOAT NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'AppliesToAll') IS NULL
                ALTER TABLE dbo.Geofences ADD AppliesToAll BIT NOT NULL CONSTRAINT DF_Geofences_AppliesToAll DEFAULT (0);
            IF COL_LENGTH(N'dbo.Geofences', N'DwellMinutes') IS NULL
                ALTER TABLE dbo.Geofences ADD DwellMinutes INT NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'SpeedLimitKmh') IS NULL
                ALTER TABLE dbo.Geofences ADD SpeedLimitKmh DECIMAL(6,1) NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'IdleMinutes') IS NULL
                ALTER TABLE dbo.Geofences ADD IdleMinutes INT NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'NotArrivedBy') IS NULL
                ALTER TABLE dbo.Geofences ADD NotArrivedBy TIME(0) NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'EngineBlockOnExit') IS NULL
                ALTER TABLE dbo.Geofences ADD EngineBlockOnExit BIT NOT NULL CONSTRAINT DF_Geofences_EngineBlockOnExit DEFAULT (0);
            -- NULL days = rules always active. Bitmask: Mon 1, Tue 2, Wed 4, Thu 8, Fri 16, Sat 32, Sun 64.
            IF COL_LENGTH(N'dbo.Geofences', N'ScheduleDays') IS NULL
                ALTER TABLE dbo.Geofences ADD ScheduleDays TINYINT NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'ScheduleStart') IS NULL
                ALTER TABLE dbo.Geofences ADD ScheduleStart TIME(0) NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'ScheduleEnd') IS NULL
                ALTER TABLE dbo.Geofences ADD ScheduleEnd TIME(0) NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'AfterHoursExitCritical') IS NULL
                ALTER TABLE dbo.Geofences ADD AfterHoursExitCritical BIT NOT NULL CONSTRAINT DF_Geofences_AfterHoursExitCritical DEFAULT (0);
            -- Comma list of InApp, WhatsApp, Email, Sms, Push, DriverPush. NULL = the alert's default channels.
            IF COL_LENGTH(N'dbo.Geofences', N'NotifyChannels') IS NULL
                ALTER TABLE dbo.Geofences ADD NotifyChannels NVARCHAR(100) NULL;
            IF COL_LENGTH(N'dbo.Geofences', N'CooldownMinutes') IS NULL
                ALTER TABLE dbo.Geofences ADD CooldownMinutes INT NOT NULL CONSTRAINT DF_Geofences_CooldownMinutes DEFAULT (15);
            """, cancellationToken: cancellationToken));

        // ── Groups and custom types ───────────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.GeofenceGroups', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.GeofenceGroups (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GeofenceGroups PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Name NVARCHAR(100) NOT NULL,
                    SortOrder INT NOT NULL CONSTRAINT DF_GeofenceGroups_SortOrder DEFAULT (0),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_GeofenceGroups_CreatedAt DEFAULT (GETUTCDATE()),
                    CreatedBy NVARCHAR(100) NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_GeofenceGroups_IsDeleted DEFAULT (0)
                );
                CREATE UNIQUE INDEX UX_GeofenceGroups_TenantName ON dbo.GeofenceGroups(TenantId, Name) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.GeofenceTypes', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.GeofenceTypes (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GeofenceTypes PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Name NVARCHAR(50) NOT NULL,
                    Color NVARCHAR(20) NOT NULL,
                    IsRestricted BIT NOT NULL CONSTRAINT DF_GeofenceTypes_IsRestricted DEFAULT (0),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_GeofenceTypes_CreatedAt DEFAULT (GETUTCDATE()),
                    CreatedBy NVARCHAR(100) NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_GeofenceTypes_IsDeleted DEFAULT (0)
                );
                CREATE UNIQUE INDEX UX_GeofenceTypes_TenantName ON dbo.GeofenceTypes(TenantId, Name) WHERE IsDeleted = 0;
            END
            """, cancellationToken: cancellationToken));

        // ── Recipients ────────────────────────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.GeofenceRecipients', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.GeofenceRecipients (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GeofenceRecipients PRIMARY KEY,
                    TenantId INT NOT NULL,
                    GeofenceId INT NOT NULL CONSTRAINT FK_GeofenceRecipients_Geofences REFERENCES dbo.Geofences(Id),
                    RoleId INT NULL,
                    UserId INT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_GeofenceRecipients_CreatedAt DEFAULT (GETUTCDATE()),
                    CONSTRAINT CK_GeofenceRecipients_OneTarget CHECK (
                        (CASE WHEN RoleId IS NULL THEN 0 ELSE 1 END) + (CASE WHEN UserId IS NULL THEN 0 ELSE 1 END) = 1)
                );
                CREATE INDEX IX_GeofenceRecipients_Geofence ON dbo.GeofenceRecipients(TenantId, GeofenceId);
            END
            """, cancellationToken: cancellationToken));

        // ── Driver-scoped assignments (the one-scope check must count DriverId too) ──
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'dbo.GeofenceAssignments', N'DriverId') IS NULL
                ALTER TABLE dbo.GeofenceAssignments ADD DriverId INT NULL;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM sys.check_constraints
                       WHERE name = N'CK_GeofenceAssignments_OneScope' AND definition NOT LIKE N'%DriverId%')
                ALTER TABLE dbo.GeofenceAssignments DROP CONSTRAINT CK_GeofenceAssignments_OneScope;

            IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_GeofenceAssignments_OneScope')
                ALTER TABLE dbo.GeofenceAssignments ADD CONSTRAINT CK_GeofenceAssignments_OneScope CHECK (
                    (CASE WHEN VehicleId IS NOT NULL THEN 1 ELSE 0 END
                   + CASE WHEN BranchId IS NOT NULL THEN 1 ELSE 0 END
                   + CASE WHEN DepartmentId IS NOT NULL THEN 1 ELSE 0 END
                   + CASE WHEN DriverId IS NOT NULL THEN 1 ELSE 0 END) = 1);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_GeofenceAssignments_Driver')
                CREATE UNIQUE INDEX UX_GeofenceAssignments_Driver
                ON dbo.GeofenceAssignments(GeofenceId, DriverId)
                WHERE IsDeleted = 0 AND DriverId IS NOT NULL;
            """, cancellationToken: cancellationToken));

        // ── Visits (one row per enter → exit) ─────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.GeofenceVisits', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.GeofenceVisits (
                    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GeofenceVisits PRIMARY KEY,
                    TenantId INT NOT NULL,
                    GeofenceId INT NOT NULL CONSTRAINT FK_GeofenceVisits_Geofences REFERENCES dbo.Geofences(Id),
                    VehicleId INT NOT NULL CONSTRAINT FK_GeofenceVisits_Vehicles REFERENCES dbo.Vehicles(Id),
                    DriverId INT NULL,
                    EnteredUtc DATETIME2 NOT NULL,
                    ExitedUtc DATETIME2 NULL,
                    LastPositionUtc DATETIME2 NOT NULL,
                    IdleSeconds INT NOT NULL CONSTRAINT DF_GeofenceVisits_IdleSeconds DEFAULT (0),
                    IdleSinceUtc DATETIME2 NULL,
                    MaxSpeedKmh DECIMAL(6,1) NOT NULL CONSTRAINT DF_GeofenceVisits_MaxSpeed DEFAULT (0),
                    DwellBreached BIT NOT NULL CONSTRAINT DF_GeofenceVisits_Dwell DEFAULT (0),
                    SpeedBreached BIT NOT NULL CONSTRAINT DF_GeofenceVisits_Speed DEFAULT (0),
                    IdleBreached BIT NOT NULL CONSTRAINT DF_GeofenceVisits_Idle DEFAULT (0),
                    AfterHoursExit BIT NOT NULL CONSTRAINT DF_GeofenceVisits_AfterHours DEFAULT (0),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_GeofenceVisits_CreatedAt DEFAULT (GETUTCDATE())
                );
                CREATE INDEX IX_GeofenceVisits_Tenant_Geofence_Entered
                    ON dbo.GeofenceVisits(TenantId, GeofenceId, EnteredUtc) INCLUDE (VehicleId, ExitedUtc);
                CREATE INDEX IX_GeofenceVisits_Tenant_Entered
                    ON dbo.GeofenceVisits(TenantId, EnteredUtc) INCLUDE (GeofenceId, VehicleId, ExitedUtc);
                -- At most one open visit per vehicle and geofence.
                CREATE UNIQUE INDEX UX_GeofenceVisits_Open
                    ON dbo.GeofenceVisits(VehicleId, GeofenceId) WHERE ExitedUtc IS NULL;
            END

            IF OBJECT_ID(N'dbo.GeofenceNotArrivedLog', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.GeofenceNotArrivedLog (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GeofenceNotArrivedLog PRIMARY KEY,
                    TenantId INT NOT NULL,
                    GeofenceId INT NOT NULL CONSTRAINT FK_GeofenceNotArrivedLog_Geofences REFERENCES dbo.Geofences(Id),
                    LocalDate DATE NOT NULL,
                    AlertedUtc DATETIME2 NOT NULL CONSTRAINT DF_GeofenceNotArrivedLog_AlertedUtc DEFAULT (GETUTCDATE())
                );
                CREATE UNIQUE INDEX UX_GeofenceNotArrivedLog_Day ON dbo.GeofenceNotArrivedLog(GeofenceId, LocalDate);
                CREATE INDEX IX_GeofenceNotArrivedLog_Tenant ON dbo.GeofenceNotArrivedLog(TenantId, AlertedUtc);
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("GeofenceWorkspaceMigration applied.");
    }
}
