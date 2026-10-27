using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Smart trip assignment decisions (explainability + reject/reassign flow).
/// </summary>
public static class TripSmartAssignmentMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF OBJECT_ID(N'dbo.TripSmartAssignmentDecisions', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.TripSmartAssignmentDecisions (
                        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripSmartAssignmentDecisions PRIMARY KEY,
                        TenantId INT NOT NULL,
                        TripId INT NOT NULL,
                        DriverId INT NOT NULL,
                        VehicleId INT NULL,
                        Rank INT NOT NULL,
                        TotalScore DECIMAL(6,2) NOT NULL,
                        ScoreBreakdownJson NVARCHAR(MAX) NOT NULL,
                        RouteId INT NULL,
                        DistanceKm DECIMAL(10,2) NULL,
                        EtaMinutes INT NULL,
                        TrafficSource NVARCHAR(40) NOT NULL,
                        Outcome NVARCHAR(30) NOT NULL,
                        RejectionReason NVARCHAR(500) NULL,
                        CreatedBy NVARCHAR(100) NULL,
                        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_TripSmartAssignmentDecisions_CreatedAt DEFAULT SYSUTCDATETIME(),
                        CONSTRAINT FK_TripSmartAssignmentDecisions_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
                    );
                    CREATE INDEX IX_TripSmartAssignmentDecisions_Tenant_Trip
                        ON dbo.TripSmartAssignmentDecisions (TenantId, TripId, CreatedAtUtc DESC);
                    CREATE INDEX IX_TripSmartAssignmentDecisions_Tenant_Created
                        ON dbo.TripSmartAssignmentDecisions (TenantId, CreatedAtUtc DESC);
                END
                """, cancellationToken: cancellationToken));

            logger.LogInformation("TripSmartAssignmentMigration applied.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TripSmartAssignmentMigration failed.");
            throw;
        }
    }
}
