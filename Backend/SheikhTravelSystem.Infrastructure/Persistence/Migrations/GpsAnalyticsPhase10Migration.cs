using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 10 Analytics & BI — a per-vehicle daily rollup table (backs long-range trend charts
/// without scanning 90-day-purged GpsPositions or recomputing from GpsTrips on every request) and
/// a scheduled-reports table mirroring MaintenanceReportSchedules.
/// </summary>
public static class GpsAnalyticsPhase10Migration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsVehicleDailyStats')
                CREATE TABLE GpsVehicleDailyStats (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NOT NULL,
                    VehicleId INT NOT NULL,
                    StatDate DATE NOT NULL,
                    DistanceKm DECIMAL(10,2) NOT NULL DEFAULT 0,
                    TripCount INT NOT NULL DEFAULT 0,
                    DrivingMinutes INT NOT NULL DEFAULT 0,
                    AvgSpeedKmh DECIMAL(10,2) NULL,
                    MaxSpeedKmh DECIMAL(10,2) NULL,
                    OverspeedCount INT NULL,
                    HarshBrakeCount INT NULL,
                    HarshAccelCount INT NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL,
                    CONSTRAINT FK_GpsVehicleDailyStats_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
                );
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_GpsVehicleDailyStats_Vehicle_Date')
                    CREATE UNIQUE INDEX UX_GpsVehicleDailyStats_Vehicle_Date ON GpsVehicleDailyStats(VehicleId, StatDate);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsVehicleDailyStats_Tenant_Date')
                    CREATE INDEX IX_GpsVehicleDailyStats_Tenant_Date ON GpsVehicleDailyStats(TenantId, StatDate);
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsAnalyticsReportSchedules')
                CREATE TABLE GpsAnalyticsReportSchedules (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NULL,
                    ReportType NVARCHAR(60) NOT NULL,
                    FiltersJson NVARCHAR(MAX) NULL,
                    Frequency NVARCHAR(20) NOT NULL DEFAULT N'Weekly',
                    Recipients NVARCHAR(1000) NOT NULL,
                    NextRunAt DATETIME2 NULL,
                    LastRunAt DATETIME2 NULL,
                    LastRunStatus NVARCHAR(30) NULL DEFAULT N'Pending',
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL,
                    CreatedBy NVARCHAR(100) NULL,
                    UpdatedBy NVARCHAR(100) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0
                );
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsAnalyticsReportSchedules_Tenant')
                    CREATE INDEX IX_GpsAnalyticsReportSchedules_Tenant ON GpsAnalyticsReportSchedules(TenantId) WHERE IsDeleted = 0;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsAnalyticsPhase10Migration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsAnalyticsPhase10Migration failed.");
        }
    }
}
