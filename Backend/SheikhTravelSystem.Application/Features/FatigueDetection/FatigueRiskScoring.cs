namespace SheikhTravelSystem.Application.Features.FatigueDetection;

public static class FatigueRiskScoring
{
    /// <summary>
    /// Combines camera/dashcam signal strength with optional AiPredictions driver_risk hook (0–1).
    /// OBD supporting signals nudge the composite when harsh patterns appear.
    /// </summary>
    public static decimal ComputeComposite(
        decimal? signalConfidence,
        decimal? driverRiskScore,
        FatigueObdSupportingSignals? obd)
    {
        var camera = Math.Clamp(signalConfidence ?? 0.55m, 0m, 1m);
        var risk = Math.Clamp(driverRiskScore ?? 0.35m, 0m, 1m);
        var obdBoost = 0m;
        if (obd is not null)
        {
            if (obd.HardBrakingEvents15Min is >= 3)
                obdBoost += 0.12m;
            if (obd.ProlongedHighSpeed == true)
                obdBoost += 0.08m;
            if (obd.SpeedKmh is >= 100)
                obdBoost += 0.05m;
        }

        var composite = camera * 0.55m + risk * 0.35m + obdBoost;
        return Math.Clamp(composite, 0m, 1m);
    }
}

public record FatigueObdSupportingSignals(
    decimal? SpeedKmh = null,
    int? HardBrakingEvents15Min = null,
    bool? ProlongedHighSpeed = null);
