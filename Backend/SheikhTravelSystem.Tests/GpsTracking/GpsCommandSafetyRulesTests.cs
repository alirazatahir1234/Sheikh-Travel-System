using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsCommandSafetyRulesTests
{
    [Fact]
    public void EvaluateEngineCutoff_allows_stationary_vehicle_even_when_fleet_ignition_would_be_off()
    {
        // BWY 217-style: speed 0 / parked bucket / Ignition forced false by motion heuristic.
        // Immobilizer must still be allowed — ACC/status ignition is not relay state.
        var result = GpsCommandSafetyRules.EvaluateEngineCutoff(
            lastUpdate: DateTime.UtcNow,
            speedKmh: 0m);

        Assert.Null(result);
    }

    [Fact]
    public void EvaluateEngineCutoff_refuses_when_moving_too_fast()
    {
        var result = GpsCommandSafetyRules.EvaluateEngineCutoff(
            lastUpdate: DateTime.UtcNow,
            speedKmh: 20m);

        Assert.Equal(
            $"Vehicle is moving too fast (>= {GpsCommandSafetyRules.EngineCutoffMaxSpeedKmh} km/h) for a safe engine cut-off.",
            result);
    }

    [Fact]
    public void EvaluateEngineCutoff_allows_below_speed_threshold()
    {
        var result = GpsCommandSafetyRules.EvaluateEngineCutoff(
            lastUpdate: DateTime.UtcNow,
            speedKmh: 19.9m);

        Assert.Null(result);
    }

    [Fact]
    public void EvaluateEngineCutoff_refuses_when_last_update_missing()
    {
        var result = GpsCommandSafetyRules.EvaluateEngineCutoff(
            lastUpdate: null,
            speedKmh: 0m);

        Assert.Equal("Cannot verify vehicle state; refusing engine cut-off for safety.", result);
    }

    [Fact]
    public void RelayNeedsEngineSafetyCheck_only_for_engine_immobilizer_purpose()
    {
        Assert.True(GpsCommandSafetyRules.RelayNeedsEngineSafetyCheck("relayOn", "EngineImmobilizer"));
        Assert.False(GpsCommandSafetyRules.RelayNeedsEngineSafetyCheck("relayOn", "Accessory"));
        Assert.False(GpsCommandSafetyRules.RelayNeedsEngineSafetyCheck("engineStop", "EngineImmobilizer"));
    }
}
