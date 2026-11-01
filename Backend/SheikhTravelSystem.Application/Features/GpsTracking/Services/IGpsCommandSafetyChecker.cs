namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGpsCommandSafetyChecker
{
    /// <summary>Returns a rejection reason, or null if it's safe to proceed.</summary>
    Task<string?> CheckEngineCutoffPreconditionAsync(
        int? vehicleId, CancellationToken cancellationToken);
}

/// <summary>Pure relay/engine-cutoff policy — no SQL.</summary>
public static class GpsCommandSafetyRules
{
    public const decimal EngineCutoffMaxSpeedKmh = 20m;

    public static bool RelayNeedsEngineSafetyCheck(string commandType, string? relayPurpose) =>
        commandType is "relayOn" or "relayOff"
        && string.Equals(relayPurpose, "EngineImmobilizer", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Engine cut-off / immobilizer preconditions from live location.
    /// <para>
    /// Do not treat <c>VehicleCurrentLocation.Ignition == false</c> as "already off".
    /// ACC/ignition is not immobilizer relay state — RELAY,1# is often needed while parked
    /// (or when the key is off). Fleet "Parked" may use motion separately from ACC.
    /// </para>
    /// Speed is the only reliable safety gate from current location.
    /// </summary>
    public static string? EvaluateEngineCutoff(DateTime? lastUpdate, decimal? speedKmh)
    {
        if (lastUpdate is null)
            return "Cannot verify vehicle state; refusing engine cut-off for safety.";

        if (speedKmh.HasValue && speedKmh.Value >= EngineCutoffMaxSpeedKmh)
            return $"Vehicle is moving too fast (>= {EngineCutoffMaxSpeedKmh} km/h) for a safe engine cut-off.";

        return null;
    }
}
