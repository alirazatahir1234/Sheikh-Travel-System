using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Pure fleet-health scoring — keep in sync with documented Gps:FleetHealth settings.
/// Does not invent values for missing telemetry. Parked/Idle/Moving are never factors.
/// </summary>
public static class GpsFleetHealthAssessor
{
    public const string BandOptimal = "Optimal";
    public const string BandHealthy = "Healthy";
    public const string BandAttention = "Attention";
    public const string BandCritical = "Critical";
    public const string BandUnknown = "Unknown";

    public const string StateOptimal = "Optimal";
    public const string StateAttention = "Attention";
    public const string StateCritical = "Critical";
    public const string StateUnknown = "Unknown";

    public const string FactorGpsFreshness = "gpsFreshness";
    public const string FactorTrackerBattery = "trackerBattery";
    public const string FactorGsmSignal = "gsmSignal";
    public const string FactorCriticalAlerts = "criticalAlerts";
    public const string FactorMaintenance = "maintenance";
    public const string FactorInsurance = "insurance";

    public sealed record VehicleHealthInput(
        int VehicleId,
        string VehicleName,
        string RegistrationNumber,
        bool HasGpsDevice,
        DateTime? LastUpdateUtc,
        decimal? BatteryPercent,
        int? GsmSignal,
        int OpenCriticalAlertCount,
        string? MaintenanceStatus,
        string? InsuranceStatus);

    public static FleetVehicleHealthDto Assess(
        VehicleHealthInput input,
        DateTime utcNow,
        GpsFleetHealthOptions options,
        int offlineStaleMinutes)
    {
        var factors = new List<FleetHealthFactorDto>
        {
            AssessGpsFreshness(input, utcNow, offlineStaleMinutes),
            AssessBattery(input.BatteryPercent, options),
            AssessGsm(input.GsmSignal, options),
            AssessCriticalAlerts(input),
            AssessMaintenance(input.MaintenanceStatus),
            AssessInsurance(input.InsuranceStatus)
        };

        var weights = options.Weights;
        var weightByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [FactorGpsFreshness] = Math.Max(0, weights.GpsFreshness),
            [FactorTrackerBattery] = Math.Max(0, weights.TrackerBattery),
            [FactorGsmSignal] = Math.Max(0, weights.GsmSignal),
            [FactorCriticalAlerts] = Math.Max(0, weights.CriticalAlerts),
            [FactorMaintenance] = Math.Max(0, weights.Maintenance),
            [FactorInsurance] = Math.Max(0, weights.Insurance)
        };

        var known = factors.Where(f => f.State != StateUnknown).ToList();
        if (known.Count == 0)
        {
            return new FleetVehicleHealthDto(
                input.VehicleId,
                input.VehicleName,
                input.RegistrationNumber,
                BandUnknown,
                null,
                Array.Empty<string>(),
                factors);
        }

        double weightedSum = 0;
        double weightSum = 0;
        foreach (var f in known)
        {
            var w = weightByKey.GetValueOrDefault(f.Key, 0);
            if (w <= 0) continue;
            weightedSum += w * FactorScore(f.State, options.FactorScores);
            weightSum += w;
        }

        if (weightSum <= 0)
        {
            return new FleetVehicleHealthDto(
                input.VehicleId,
                input.VehicleName,
                input.RegistrationNumber,
                BandUnknown,
                null,
                Array.Empty<string>(),
                factors);
        }

        var score = (int)Math.Round(weightedSum / weightSum, MidpointRounding.AwayFromZero);
        var band = ResolveBand(score, known, options.Bands);
        var reasons = known
            .Where(f => f.State != StateOptimal)
            .Select(f => f.Detail)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FleetVehicleHealthDto(
            input.VehicleId,
            input.VehicleName,
            input.RegistrationNumber,
            band,
            score,
            reasons,
            factors);
    }

    /// <summary>
    /// Score sets the baseline band; any Critical factor forces Critical, and any Attention
    /// factor prevents Optimal/Healthy so reasons surface in Attention.
    /// </summary>
    private static string ResolveBand(
        int score,
        IReadOnlyList<FleetHealthFactorDto> known,
        GpsFleetHealthBands bands)
    {
        if (known.Any(f => f.State == StateCritical))
            return BandCritical;

        var scoreBand = BandForScore(score, bands);
        if (known.Any(f => f.State == StateAttention) &&
            (scoreBand == BandOptimal || scoreBand == BandHealthy))
        {
            return BandAttention;
        }

        return scoreBand;
    }

    public static FleetHealthSummaryDto Summarize(IReadOnlyList<FleetVehicleHealthDto> vehicles)
    {
        var optimal = vehicles.Count(v => v.Band == BandOptimal);
        var healthy = vehicles.Count(v => v.Band == BandHealthy);
        var attention = vehicles.Count(v => v.Band == BandAttention);
        var critical = vehicles.Count(v => v.Band == BandCritical);
        var unknown = vehicles.Count(v => v.Band == BandUnknown);
        var assessed = optimal + healthy + attention + critical;
        int? assessedPercent = null;
        if (assessed > 0)
        {
            var scores = vehicles.Where(v => v.Score.HasValue).Select(v => v.Score!.Value).ToList();
            assessedPercent = scores.Count == 0
                ? null
                : (int)Math.Round(scores.Average(), MidpointRounding.AwayFromZero);
        }

        return new FleetHealthSummaryDto(
            assessedPercent,
            optimal,
            healthy,
            attention,
            critical,
            unknown,
            vehicles.Count,
            assessed,
            vehicles);
    }

    private static int FactorScore(string state, GpsFleetHealthFactorScores scores) => state switch
    {
        StateCritical => scores.Critical,
        StateAttention => scores.Attention,
        StateOptimal => scores.Optimal,
        _ => scores.Optimal
    };

    private static string BandForScore(int score, GpsFleetHealthBands bands)
    {
        if (score >= bands.OptimalMin) return BandOptimal;
        if (score >= bands.HealthyMin) return BandHealthy;
        if (score >= bands.AttentionMin) return BandAttention;
        return BandCritical;
    }

    private static FleetHealthFactorDto AssessGpsFreshness(
        VehicleHealthInput input,
        DateTime utcNow,
        int offlineStaleMinutes)
    {
        if (!input.HasGpsDevice || input.LastUpdateUtc is null)
        {
            return new FleetHealthFactorDto(
                FactorGpsFreshness,
                StateUnknown,
                input.HasGpsDevice ? "No GPS fix yet" : "No GPS device");
        }

        var ageMinutes = (utcNow - input.LastUpdateUtc.Value).TotalMinutes;
        if (ageMinutes > offlineStaleMinutes)
        {
            return new FleetHealthFactorDto(
                FactorGpsFreshness,
                StateAttention,
                "GPS stale");
        }

        return new FleetHealthFactorDto(
            FactorGpsFreshness,
            StateOptimal,
            "GPS fresh");
    }

    private static FleetHealthFactorDto AssessBattery(decimal? battery, GpsFleetHealthOptions options)
    {
        if (battery is null)
            return new FleetHealthFactorDto(FactorTrackerBattery, StateUnknown, "Battery unknown");

        if (battery.Value < options.CriticalBatteryPercent)
            return new FleetHealthFactorDto(FactorTrackerBattery, StateCritical, "Battery critically low");

        if (battery.Value < options.AttentionBatteryPercent)
            return new FleetHealthFactorDto(FactorTrackerBattery, StateAttention, "Battery low");

        return new FleetHealthFactorDto(FactorTrackerBattery, StateOptimal, "Battery OK");
    }

    private static FleetHealthFactorDto AssessGsm(int? gsm, GpsFleetHealthOptions options)
    {
        if (gsm is null)
            return new FleetHealthFactorDto(FactorGsmSignal, StateUnknown, "GSM unknown");

        if (gsm.Value < options.AttentionGsmSignal)
            return new FleetHealthFactorDto(FactorGsmSignal, StateAttention, "Weak GSM signal");

        return new FleetHealthFactorDto(FactorGsmSignal, StateOptimal, "GSM OK");
    }

    private static FleetHealthFactorDto AssessCriticalAlerts(VehicleHealthInput input)
    {
        // Without a tracker there is no meaningful critical GPS-alert signal — Unknown, not "healthy".
        if (!input.HasGpsDevice)
        {
            return new FleetHealthFactorDto(
                FactorCriticalAlerts,
                StateUnknown,
                "Alerts unavailable (no GPS device)");
        }

        if (input.OpenCriticalAlertCount > 0)
        {
            return new FleetHealthFactorDto(
                FactorCriticalAlerts,
                StateCritical,
                input.OpenCriticalAlertCount == 1
                    ? "Critical alert open"
                    : $"{input.OpenCriticalAlertCount} critical alerts open");
        }

        return new FleetHealthFactorDto(FactorCriticalAlerts, StateOptimal, "No critical alerts");
    }

    private static FleetHealthFactorDto AssessMaintenance(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) ||
            string.Equals(status, "None", StringComparison.OrdinalIgnoreCase))
        {
            return new FleetHealthFactorDto(FactorMaintenance, StateUnknown, "No maintenance schedule");
        }

        if (string.Equals(status, MaintenanceScheduleHelper.StatusOverdue, StringComparison.OrdinalIgnoreCase))
            return new FleetHealthFactorDto(FactorMaintenance, StateCritical, "Maintenance overdue");

        if (string.Equals(status, MaintenanceScheduleHelper.StatusDueSoon, StringComparison.OrdinalIgnoreCase))
            return new FleetHealthFactorDto(FactorMaintenance, StateAttention, "Maintenance due soon");

        return new FleetHealthFactorDto(FactorMaintenance, StateOptimal, "Maintenance on track");
    }

    private static FleetHealthFactorDto AssessInsurance(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) ||
            string.Equals(status, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return new FleetHealthFactorDto(FactorInsurance, StateUnknown, "Insurance unknown");
        }

        if (string.Equals(status, "Expired", StringComparison.OrdinalIgnoreCase))
            return new FleetHealthFactorDto(FactorInsurance, StateCritical, "Insurance expired");

        if (string.Equals(status, "ExpiringSoon", StringComparison.OrdinalIgnoreCase))
            return new FleetHealthFactorDto(FactorInsurance, StateAttention, "Insurance expiring soon");

        return new FleetHealthFactorDto(FactorInsurance, StateOptimal, "Insurance valid");
    }
}
