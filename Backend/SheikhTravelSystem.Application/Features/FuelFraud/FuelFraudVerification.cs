namespace SheikhTravelSystem.Application.Features.FuelFraud;

/// <summary>
/// Compares driver-ordered liters at the pump to OBD fuel-level delta (tank % × capacity).
/// Uses real telemetry only — never fabricates levels.
/// </summary>
public static class FuelFraudVerification
{
    public const string StatusPending = "PendingVerification";
    public const string StatusMatched = "Matched";
    public const string StatusFraudAlert = "FraudAlert";
    public const string StatusNoObd = "NoObdData";
    public const string StatusResolved = "Resolved";

    public record Result(
        string Status,
        decimal? ActualIncreaseLiters,
        decimal? ShortfallLiters,
        decimal? FuelLevelAfterPercent);

    /// <param name="toleranceLiters">Allowed under-delivery before raising fraud (sensor noise).</param>
    public static Result Evaluate(
        decimal orderedLiters,
        decimal? fuelLevelBeforePercent,
        decimal? fuelLevelAfterPercent,
        decimal? tankCapacityLiters,
        decimal toleranceLiters = 1.5m)
    {
        if (orderedLiters <= 0)
            return new Result(StatusNoObd, null, null, fuelLevelAfterPercent);

        if (!fuelLevelBeforePercent.HasValue || !fuelLevelAfterPercent.HasValue)
            return new Result(StatusNoObd, null, null, fuelLevelAfterPercent);

        if (tankCapacityLiters is not > 0)
            return new Result(StatusNoObd, null, null, fuelLevelAfterPercent);

        var deltaPercent = fuelLevelAfterPercent.Value - fuelLevelBeforePercent.Value;
        if (deltaPercent < 0)
            deltaPercent = 0;

        var actualLiters = Math.Round(deltaPercent / 100m * tankCapacityLiters.Value, 2);
        var shortfall = Math.Round(orderedLiters - actualLiters, 2);

        if (shortfall <= toleranceLiters)
            return new Result(StatusMatched, actualLiters, shortfall <= 0 ? 0 : shortfall, fuelLevelAfterPercent);

        return new Result(StatusFraudAlert, actualLiters, shortfall, fuelLevelAfterPercent);
    }
}
