using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Features.Drivers.Services;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Drivers;

public sealed class DriverTelematicsEnforcementService(
    ILogger<DriverTelematicsEnforcementService> logger) : IDriverTelematicsEnforcementService
{
    public async Task ProcessAlertAsync(
        IDbConnection connection, int gpsAlertId, CancellationToken cancellationToken = default)
    {
        var alert = await connection.QuerySingleOrDefaultAsync<(
            int Id, int VehicleId, int? DriverId, string EventType, DateTime Timestamp)>(
            new CommandDefinition(
                """
                SELECT Id, VehicleId, DriverId, EventType, Timestamp
                FROM GpsAlertEvents
                WHERE Id = @Id AND IsDeleted = 0
                """,
                new { Id = gpsAlertId },
                cancellationToken: cancellationToken));

        if (alert.Id == 0 || !DriverTelematicsEnforcementPolicy.ShouldCreateViolation(alert.EventType))
            return;

        if (!alert.DriverId.HasValue)
        {
            logger.LogDebug(
                "Telematics alert {AlertId} ({EventType}) has no DriverId; skipping violation.",
                gpsAlertId, alert.EventType);
            return;
        }

        var tenantId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Vehicles WHERE Id = @VehicleId AND IsDeleted = 0",
            new { alert.VehicleId },
            cancellationToken: cancellationToken));

        if (tenantId is null or 0)
            return;

        var alreadyLinked = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM DriverViolations
                WHERE GpsAlertId = @GpsAlertId AND IsDeleted = 0
            ) THEN 1 ELSE 0 END
            """,
            new { GpsAlertId = gpsAlertId },
            cancellationToken: cancellationToken));

        if (alreadyLinked)
            return;

        var violationType = DriverTelematicsEnforcementPolicy.ViolationTypeForEvent(alert.EventType);
        var severity = DriverTelematicsEnforcementPolicy.SeverityForEvent(alert.EventType);
        var description =
            $"Auto-created from GPS alert #{gpsAlertId} ({alert.EventType}). " +
            $"Suspend policy: {DriverTelematicsEnforcementPolicy.HarshViolationSuspendThreshold} harsh " +
            $"violations in {DriverTelematicsEnforcementPolicy.RollingWindowDays} days.";

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO DriverViolations
              (TenantId, DriverId, ViolationType, Severity, OccurredAt, Description,
               GpsAlertId, Status, CreatedBy, CreatedAt, IsDeleted)
            VALUES
              (@TenantId, @DriverId, @ViolationType, @Severity, @OccurredAt, @Description,
               @GpsAlertId, N'Open', @CreatedBy, GETUTCDATE(), 0)
            """,
            new
            {
                TenantId = tenantId.Value,
                DriverId = alert.DriverId.Value,
                ViolationType = violationType,
                Severity = severity,
                OccurredAt = alert.Timestamp,
                Description = description,
                GpsAlertId = gpsAlertId,
                CreatedBy = DriverTelematicsEnforcementPolicy.CreatedBySystem
            },
            cancellationToken: cancellationToken));

        if (!DriverTelematicsEnforcementPolicy.CountsTowardSuspend(violationType))
            return;

        var harshCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)
            FROM DriverViolations
            WHERE TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0
              AND ViolationType IN (@HarshBrake, @HarshAccel)
              AND OccurredAt >= DATEADD(DAY, -@WindowDays, GETUTCDATE())
            """,
            new
            {
                TenantId = tenantId.Value,
                DriverId = alert.DriverId.Value,
                HarshBrake = DriverTelematicsEnforcementPolicy.ViolationTypeHarshBrake,
                HarshAccel = DriverTelematicsEnforcementPolicy.ViolationTypeHarshAccel,
                WindowDays = DriverTelematicsEnforcementPolicy.RollingWindowDays
            },
            cancellationToken: cancellationToken));

        if (harshCount < DriverTelematicsEnforcementPolicy.HarshViolationSuspendThreshold)
            return;

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE Drivers
            SET Status = @Suspended, UpdatedAt = GETUTCDATE()
            WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
              AND Status NOT IN (@Suspended, @OnLeave)
            """,
            new
            {
                DriverId = alert.DriverId.Value,
                TenantId = tenantId.Value,
                Suspended = (int)DriverStatus.Suspended,
                OnLeave = (int)DriverStatus.OnLeave
            },
            cancellationToken: cancellationToken));

        if (rows > 0)
        {
            logger.LogWarning(
                "Driver {DriverId} auto-suspended after {Count} harsh violations in {Days} days (alert {AlertId}).",
                alert.DriverId.Value, harshCount, DriverTelematicsEnforcementPolicy.RollingWindowDays, gpsAlertId);
        }
    }
}
