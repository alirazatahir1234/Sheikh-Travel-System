using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Tracker registration historically set SupportsEngineCutoff + RelayPurpose/Output
/// but left SupportsRelay at DEFAULT 0. Cut Engine (engineStop → RELAY,1#) does not
/// require SupportsRelay, yet the Remote Commands strip showed "Relay: Not supported"
/// and confused operators. Sync the flag for immobilizer-wired / VG03-family devices.
/// </summary>
public static class Vg03EngineRelayCapabilityMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            var updated = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE d
                SET SupportsRelay = 1,
                    UpdatedAt = SYSUTCDATETIME()
                FROM GpsDevices d
                WHERE d.IsDeleted = 0
                  AND d.SupportsRelay = 0
                  AND d.SupportsEngineCutoff = 1
                  AND (
                      d.RelayPurpose = N'EngineImmobilizer'
                      OR d.TrackerModelKey IN (N'jimi_vg03', N'jimi_ev26r', N'jimi_gt06', N'concox_gt06n', N'sinotrack_st901', N'concox_at6')
                      OR d.Model LIKE N'%VG03%'
                      OR d.Model LIKE N'%EV26%'
                  );
                """, cancellationToken: cancellationToken));

            logger.LogInformation(
                "Vg03EngineRelayCapabilityMigration applied successfully. SupportsRelay backfilled on {Count} device(s).",
                updated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Vg03EngineRelayCapabilityMigration failed.");
            throw;
        }
    }
}
