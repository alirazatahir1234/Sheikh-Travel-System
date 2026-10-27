using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Trips tab enhancements: cargo flags, templates, timeline events, revenue estimate field.
/// </summary>
public static class TripEnhancementsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.Trips', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.Trips', 'SpecialCargoFlags') IS NULL
                    ALTER TABLE dbo.Trips ADD SpecialCargoFlags NVARCHAR(200) NULL;
                IF COL_LENGTH('dbo.Trips', 'EstimatedRevenue') IS NULL
                    ALTER TABLE dbo.Trips ADD EstimatedRevenue DECIMAL(18,2) NULL;
                IF COL_LENGTH('dbo.Trips', 'ClonedFromTripId') IS NULL
                    ALTER TABLE dbo.Trips ADD ClonedFromTripId INT NULL;
            END

            IF OBJECT_ID(N'dbo.TripTemplates', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TripTemplates (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripTemplates PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Name NVARCHAR(120) NOT NULL,
                    PayloadJson NVARCHAR(MAX) NOT NULL,
                    RecurrenceKind NVARCHAR(20) NOT NULL CONSTRAINT DF_TripTemplates_Recurrence DEFAULT (N'None'),
                    RecurrenceInterval INT NOT NULL CONSTRAINT DF_TripTemplates_Interval DEFAULT (1),
                    RecurrenceDaysOfWeek NVARCHAR(30) NULL,
                    NextOccurrenceDate DATE NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TripTemplates_Active DEFAULT (1),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TripTemplates_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy NVARCHAR(100) NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TripTemplates_IsDeleted DEFAULT (0)
                );
                CREATE INDEX IX_TripTemplates_Tenant_Active
                    ON dbo.TripTemplates (TenantId, IsActive) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TripTimelineEvents', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TripTimelineEvents (
                    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripTimelineEvents PRIMARY KEY,
                    TenantId INT NOT NULL,
                    TripId INT NOT NULL,
                    EventType NVARCHAR(40) NOT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    Detail NVARCHAR(1000) NULL,
                    OccurredAtUtc DATETIME2 NOT NULL CONSTRAINT DF_TripTimelineEvents_Occurred DEFAULT (SYSUTCDATETIME()),
                    CreatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_TripTimelineEvents_Trip
                    ON dbo.TripTimelineEvents (TenantId, TripId, OccurredAtUtc DESC);
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("TripEnhancementsMigration applied.");
    }
}
