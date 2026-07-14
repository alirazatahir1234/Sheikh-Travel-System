using Dapper;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Engine-cutoff safety precondition (spec: Ignition=ON AND Speed &lt; 20 km/h) shared by
/// SendDeviceCommandCommand and RetryDeviceCommandCommand — retry re-checks because vehicle state
/// may have changed since the original attempt.
/// </summary>
public static class GpsCommandSafetyChecker
{
    private const decimal EngineCutoffMaxSpeedKmh = 20m;

    /// <summary>Returns a rejection reason, or null if it's safe to proceed.</summary>
    public static async Task<string?> CheckEngineCutoffPreconditionAsync(
        System.Data.IDbConnection connection, int? vehicleId, CancellationToken cancellationToken)
    {
        if (!vehicleId.HasValue)
            return "Cannot verify vehicle state; refusing engine cut-off for safety.";

        // LastUpdate is NOT NULL when a row exists — used to distinguish "no current-location row"
        // from "row exists but Ignition/Speed happen to be null", since a value-tuple default for a
        // missing row also comes back as all-null and can't otherwise be told apart.
        var state = await connection.QueryFirstOrDefaultAsync<(bool? Ignition, decimal? Speed, DateTime? LastUpdate)>(
            new CommandDefinition(
                "SELECT Ignition, Speed, LastUpdate FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId",
                new { VehicleId = vehicleId.Value },
                cancellationToken: cancellationToken));

        if (state.LastUpdate is null)
            return "Cannot verify vehicle state; refusing engine cut-off for safety.";

        if (state.Ignition == false)
            return "Engine is already off.";

        if (state.Speed.HasValue && state.Speed.Value >= EngineCutoffMaxSpeedKmh)
            return $"Vehicle is moving too fast (>= {EngineCutoffMaxSpeedKmh} km/h) for a safe engine cut-off.";

        return null;
    }

    /// <summary>
    /// Whether a relayOn/relayOff command needs the engine-cutoff precondition — only when this
    /// specific device's relay is wired as an engine immobilizer. Both directions are gated
    /// conservatively since which direction actually cuts power is installation-specific and can't
    /// be reliably inferred from data alone.
    /// </summary>
    public static bool RelayNeedsEngineSafetyCheck(string commandType, string? relayPurpose) =>
        commandType is "relayOn" or "relayOff"
        && string.Equals(relayPurpose, "EngineImmobilizer", StringComparison.OrdinalIgnoreCase);
}
