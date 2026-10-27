using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Post-trip client feedback (1–5 stars × politeness / on-time / vehicle), invite links, public profile flags.
/// </summary>
public static class DriverTripFeedbackMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'TripRatings')
            BEGIN
                IF COL_LENGTH('TripRatings', 'Politeness') IS NULL
                    ALTER TABLE TripRatings ADD Politeness TINYINT NULL;
                IF COL_LENGTH('TripRatings', 'OnTimeDelivery') IS NULL
                    ALTER TABLE TripRatings ADD OnTimeDelivery TINYINT NULL;
                IF COL_LENGTH('TripRatings', 'VehicleCondition') IS NULL
                    ALTER TABLE TripRatings ADD VehicleCondition TINYINT NULL;
                IF COL_LENGTH('TripRatings', 'Comment') IS NULL
                    ALTER TABLE TripRatings ADD Comment NVARCHAR(500) NULL;
                IF COL_LENGTH('TripRatings', 'IsHidden') IS NULL
                    ALTER TABLE TripRatings ADD IsHidden BIT NOT NULL
                        CONSTRAINT DF_TripRatings_IsHidden DEFAULT (0);
            END

            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'TripFeedbackLinks')
            BEGIN
                CREATE TABLE TripFeedbackLinks (
                    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripFeedbackLinks PRIMARY KEY,
                    TenantId        INT NOT NULL,
                    TripId          INT NOT NULL,
                    TokenHash       VARBINARY(32) NOT NULL,
                    TokenProtected  NVARCHAR(512) NOT NULL,
                    ExpiresAt       DATETIME2 NOT NULL,
                    UsedAt          DATETIME2 NULL,
                    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_TripFeedbackLinks_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT UQ_TripFeedbackLinks_TokenHash UNIQUE (TokenHash)
                );
                CREATE UNIQUE INDEX UQ_TripFeedbackLinks_ActiveTrip
                    ON TripFeedbackLinks (TripId)
                    WHERE UsedAt IS NULL;
            END

            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Drivers')
            BEGIN
                IF COL_LENGTH('Drivers', 'PublicRatingOptOut') IS NULL
                    ALTER TABLE Drivers ADD PublicRatingOptOut BIT NOT NULL
                        CONSTRAINT DF_Drivers_PublicRatingOptOut DEFAULT (0);
            END
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM NotificationTemplates
                WHERE TemplateKey = N'trip_feedback_request' AND Channel = N'Email' AND IsDeleted = 0)
            INSERT INTO NotificationTemplates (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, CreatedAt, IsDeleted)
            VALUES (
                N'trip_feedback_request',
                N'Trip feedback request',
                N'Rate your trip {{TripNumber}}',
                N'{{Message}}',
                N'Email',
                1,
                GETUTCDATE(),
                0);

            IF NOT EXISTS (
                SELECT 1 FROM NotificationTemplates
                WHERE TemplateKey = N'trip_feedback_request' AND Channel = N'InApp' AND IsDeleted = 0)
            INSERT INTO NotificationTemplates (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, CreatedAt, IsDeleted)
            VALUES (
                N'trip_feedback_request',
                N'Trip feedback request',
                N'Trip feedback',
                N'{{Message}}',
                N'InApp',
                1,
                GETUTCDATE(),
                0);
            """, cancellationToken: cancellationToken));

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "FLEET_MANAGER",
            [DriverPermissions.DriverViewPerformance],
            cancellationToken);

        logger.LogInformation("DriverTripFeedbackMigration applied.");
    }
}
