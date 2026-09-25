using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 2: automation rules/events, tracking links, consents, trip ratings, PreferredLanguage.
/// </summary>
public static class WhatsAppPhase2AutomationsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        await EnsureAutomationRulesAsync(connection, cancellationToken);
        await EnsureAutomationEventsAsync(connection, cancellationToken);
        await EnsureTrackingLinksAsync(connection, cancellationToken);
        await EnsureConsentsAsync(connection, cancellationToken);
        await EnsureTripRatingsAsync(connection, cancellationToken);
        await EnsureMessageAutomationEventIdAsync(connection, cancellationToken);
        await EnsureCustomerPreferredLanguageAsync(connection, cancellationToken);
        await SeedDisabledRulesAsync(connection, cancellationToken);

        logger.LogInformation("WhatsAppPhase2AutomationsMigration applied successfully.");
    }

    private static async Task EnsureAutomationRulesAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await TableExistsAsync(connection, "WhatsAppAutomationRules", ct))
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE TABLE WhatsAppAutomationRules (
                Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WhatsAppAutomationRules PRIMARY KEY,
                TenantId        INT NOT NULL,
                EventType       NVARCHAR(64) NOT NULL,
                IsEnabled       BIT NOT NULL CONSTRAINT DF_WA_AutoRules_Enabled DEFAULT (0),
                AccountId       INT NULL,
                TemplateName    NVARCHAR(200) NULL,
                OffsetMinutes   INT NOT NULL CONSTRAINT DF_WA_AutoRules_Offset DEFAULT (0),
                NotifyBooker    BIT NOT NULL CONSTRAINT DF_WA_AutoRules_NotifyBooker DEFAULT (0),
                IsUrgent        BIT NOT NULL CONSTRAINT DF_WA_AutoRules_Urgent DEFAULT (1),
                CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_WA_AutoRules_CreatedAt DEFAULT (SYSUTCDATETIME()),
                UpdatedAt       DATETIME2 NOT NULL CONSTRAINT DF_WA_AutoRules_UpdatedAt DEFAULT (SYSUTCDATETIME()),
                CONSTRAINT UQ_WhatsAppAutomationRules_Tenant_Event UNIQUE (TenantId, EventType)
            );
            """, cancellationToken: ct));
    }

    private static async Task EnsureAutomationEventsAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await TableExistsAsync(connection, "WhatsAppAutomationEvents", ct))
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE TABLE WhatsAppAutomationEvents (
                Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WhatsAppAutomationEvents PRIMARY KEY,
                TenantId        INT NOT NULL,
                EventType       NVARCHAR(64) NOT NULL,
                BookingId       INT NULL,
                TripId          INT NULL,
                DedupeKey       NVARCHAR(200) NOT NULL,
                Status          TINYINT NOT NULL CONSTRAINT DF_WA_AutoEvents_Status DEFAULT (0),
                SkipReason      NVARCHAR(64) NULL,
                DueAt           DATETIME2 NOT NULL CONSTRAINT DF_WA_AutoEvents_DueAt DEFAULT (SYSUTCDATETIME()),
                LockedUntil     DATETIME2 NULL,
                Attempts        INT NOT NULL CONSTRAINT DF_WA_AutoEvents_Attempts DEFAULT (0),
                Error           NVARCHAR(2000) NULL,
                MessageId       INT NULL,
                RecipientPhone  NVARCHAR(32) NULL,
                PayloadJson     NVARCHAR(MAX) NULL,
                CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_WA_AutoEvents_CreatedAt DEFAULT (SYSUTCDATETIME()),
                ProcessedAt     DATETIME2 NULL,
                CONSTRAINT UQ_WhatsAppAutomationEvents_Tenant_Dedupe UNIQUE (TenantId, DedupeKey)
            );
            CREATE INDEX IX_WhatsAppAutomationEvents_Due
                ON WhatsAppAutomationEvents (DueAt)
                WHERE Status IN (0, 1);
            CREATE INDEX IX_WhatsAppAutomationEvents_Booking
                ON WhatsAppAutomationEvents (TenantId, BookingId);
            """, cancellationToken: ct));
    }

    private static async Task EnsureTrackingLinksAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await TableExistsAsync(connection, "TripTrackingLinks", ct))
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE TABLE TripTrackingLinks (
                Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripTrackingLinks PRIMARY KEY,
                TenantId        INT NOT NULL,
                TripId          INT NOT NULL,
                BookingId       INT NULL,
                TokenHash       BINARY(32) NOT NULL,
                TokenProtected  NVARCHAR(500) NOT NULL,
                ExpiresAt       DATETIME2 NOT NULL,
                RevokedAt       DATETIME2 NULL,
                ViewCount       INT NOT NULL CONSTRAINT DF_TripTrackingLinks_Views DEFAULT (0),
                LastViewedAt    DATETIME2 NULL,
                CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_TripTrackingLinks_CreatedAt DEFAULT (SYSUTCDATETIME()),
                CONSTRAINT UQ_TripTrackingLinks_TokenHash UNIQUE (TokenHash)
            );
            CREATE UNIQUE INDEX UQ_TripTrackingLinks_ActiveTrip
                ON TripTrackingLinks (TripId)
                WHERE RevokedAt IS NULL;
            """, cancellationToken: ct));
    }

    private static async Task EnsureConsentsAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await TableExistsAsync(connection, "WhatsAppContactConsents", ct))
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE TABLE WhatsAppContactConsents (
                Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WhatsAppContactConsents PRIMARY KEY,
                TenantId        INT NOT NULL,
                WaId            NVARCHAR(32) NOT NULL,
                OptInAt         DATETIME2 NULL,
                OptInSource     NVARCHAR(40) NULL,
                OptOutAt        DATETIME2 NULL,
                Note            NVARCHAR(500) NULL,
                CreatedByUserId INT NULL,
                CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_WA_Consents_CreatedAt DEFAULT (SYSUTCDATETIME()),
                UpdatedAt       DATETIME2 NOT NULL CONSTRAINT DF_WA_Consents_UpdatedAt DEFAULT (SYSUTCDATETIME()),
                CONSTRAINT UQ_WhatsAppContactConsents_Tenant_Wa UNIQUE (TenantId, WaId)
            );
            """, cancellationToken: ct));
    }

    private static async Task EnsureTripRatingsAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await TableExistsAsync(connection, "TripRatings", ct))
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE TABLE TripRatings (
                Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripRatings PRIMARY KEY,
                TenantId        INT NOT NULL,
                TripId          INT NOT NULL,
                DriverId        INT NULL,
                Score           TINYINT NOT NULL,
                Source          NVARCHAR(40) NOT NULL CONSTRAINT DF_TripRatings_Source DEFAULT (N'WhatsApp'),
                MessageId       INT NULL,
                CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_TripRatings_CreatedAt DEFAULT (SYSUTCDATETIME()),
                CONSTRAINT UQ_TripRatings_Tenant_Trip UNIQUE (TenantId, TripId)
            );
            """, cancellationToken: ct));
    }

    private static async Task EnsureMessageAutomationEventIdAsync(IDbConnection connection, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, "WhatsAppMessages", ct))
            return;

        if (!await ColumnExistsAsync(connection, "WhatsAppMessages", "AutomationEventId", ct))
        {
            // Must be its own batch — SQL Server compiles CREATE INDEX against the pre-ALTER schema.
            await connection.ExecuteAsync(new CommandDefinition("""
                ALTER TABLE WhatsAppMessages ADD AutomationEventId BIGINT NULL;
                """, cancellationToken: ct));
        }

        var indexExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_WhatsAppMessages_AutomationEventId'
                  AND object_id = OBJECT_ID(N'dbo.WhatsAppMessages')
            ) THEN 1 ELSE 0 END
            """, cancellationToken: ct));

        if (indexExists == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                CREATE INDEX IX_WhatsAppMessages_AutomationEventId
                    ON WhatsAppMessages (AutomationEventId)
                    WHERE AutomationEventId IS NOT NULL;
                """, cancellationToken: ct));
        }
    }

    private static async Task EnsureCustomerPreferredLanguageAsync(IDbConnection connection, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, "Customers", ct))
            return;
        if (await ColumnExistsAsync(connection, "Customers", "PreferredLanguage", ct))
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
            ALTER TABLE Customers ADD PreferredLanguage NVARCHAR(8) NULL;
            """, cancellationToken: ct));
    }

    private static async Task SeedDisabledRulesAsync(IDbConnection connection, CancellationToken ct)
    {
        var eventTypes = new[]
        {
            "BookingConfirmed", "BookingRescheduled", "BookingCancelled", "PickupReminder",
            "DriverAssigned", "DriverEnRoute", "DriverArriving", "DriverArrived",
            "TripCompleted", "RentalReturnReminder"
        };

        // Seed for every known tenant that has WhatsApp accounts (or TenantId=1 fallback).
        var tenantIds = (await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT DISTINCT TenantId FROM WhatsAppAccounts
            UNION
            SELECT 1
            """, cancellationToken: ct))).Distinct().ToList();

        foreach (var tenantId in tenantIds)
        {
            foreach (var eventType in eventTypes)
            {
                var isUrgent = eventType is not "RentalReturnReminder";
                var offset = eventType switch
                {
                    "PickupReminder" => -60,
                    "TripCompleted" => 2,
                    "RentalReturnReminder" => -1440,
                    _ => 0
                };
                var template = eventType switch
                {
                    "BookingConfirmed" => "booking_confirmed",
                    "BookingRescheduled" => "booking_rescheduled",
                    "BookingCancelled" => "booking_cancelled",
                    "PickupReminder" => "pickup_reminder",
                    "DriverAssigned" => "driver_assigned",
                    "DriverEnRoute" => "driver_en_route",
                    "DriverArriving" => "driver_arriving",
                    "DriverArrived" => "driver_arrived",
                    "TripCompleted" => "trip_completed",
                    "RentalReturnReminder" => "rental_return_reminder",
                    _ => eventType.ToLowerInvariant()
                };

                await connection.ExecuteAsync(new CommandDefinition("""
                    IF NOT EXISTS (
                        SELECT 1 FROM WhatsAppAutomationRules
                        WHERE TenantId = @TenantId AND EventType = @EventType)
                    INSERT INTO WhatsAppAutomationRules
                        (TenantId, EventType, IsEnabled, TemplateName, OffsetMinutes, NotifyBooker, IsUrgent)
                    VALUES
                        (@TenantId, @EventType, 0, @TemplateName, @OffsetMinutes, 0, @IsUrgent);
                    """, new
                    {
                        TenantId = tenantId,
                        EventType = eventType,
                        TemplateName = template,
                        OffsetMinutes = offset,
                        IsUrgent = isUrgent
                    }, cancellationToken: ct));
            }
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        IDbConnection connection, string table, string column, CancellationToken ct)
    {
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN COL_LENGTH(@Table, @Column) IS NULL THEN 0 ELSE 1 END
            """, new { Table = table, Column = column }, cancellationToken: ct));
        return n == 1;
    }

    private static async Task<bool> TableExistsAsync(IDbConnection connection, string table, CancellationToken ct)
    {
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(@Table, N'U') IS NULL THEN 0 ELSE 1 END
            """, new { Table = table }, cancellationToken: ct));
        return n == 1;
    }
}
