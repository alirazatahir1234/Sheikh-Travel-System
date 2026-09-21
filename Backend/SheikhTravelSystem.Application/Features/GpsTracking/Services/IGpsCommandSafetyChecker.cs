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
    public static bool RelayNeedsEngineSafetyCheck(string commandType, string? relayPurpose) =>
        commandType is "relayOn" or "relayOff"
        && string.Equals(relayPurpose, "EngineImmobilizer", StringComparison.OrdinalIgnoreCase);
}
