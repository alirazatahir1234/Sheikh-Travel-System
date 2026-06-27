using System.Data;
using Dapper;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Updates GpsDevices telemetry columns without full position ingest (for unlinked devices).
/// </summary>
public static class GpsDeviceTelemetryUpdater
{
    public static Task UpdateAsync(
        IDbConnection connection,
        int gpsDeviceId,
        DateTime timestamp,
        bool? ignition,
        CancellationToken cancellationToken = default)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE GpsDevices SET LastSeenAt = @Timestamp, LastIgnition = @Ignition, UpdatedAt = @Timestamp
              WHERE Id = @Id AND IsDeleted = 0",
            new { Id = gpsDeviceId, Timestamp = timestamp, Ignition = ignition },
            cancellationToken: cancellationToken));
    }
}
