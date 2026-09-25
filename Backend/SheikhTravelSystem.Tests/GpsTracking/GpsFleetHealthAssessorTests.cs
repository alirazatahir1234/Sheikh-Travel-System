using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsFleetHealthAssessorTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly GpsFleetHealthOptions Options = new();

    private static GpsFleetHealthAssessor.VehicleHealthInput BaseInput(
        Action<GpsFleetHealthAssessor.VehicleHealthInput>? _ = null) =>
        new(
            VehicleId: 1,
            VehicleName: "Toyota Corolla",
            RegistrationNumber: "ABC-123",
            HasGpsDevice: true,
            LastUpdateUtc: Now.AddMinutes(-2),
            BatteryPercent: 80m,
            GsmSignal: 25,
            OpenCriticalAlertCount: 0,
            MaintenanceStatus: MaintenanceScheduleHelper.StatusUpcoming,
            InsuranceStatus: "Valid");

    [Fact]
    public void Parked_vehicle_with_good_factors_is_not_attention()
    {
        // Operational Parked is irrelevant — assessor has no operational status input.
        var result = GpsFleetHealthAssessor.Assess(BaseInput(), Now, Options, offlineStaleMinutes: 30);
        Assert.True(result.Band is GpsFleetHealthAssessor.BandOptimal or GpsFleetHealthAssessor.BandHealthy);
        Assert.DoesNotContain(result.Reasons, r => r.Contains("Parked", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(result.Score);
        Assert.True(result.Score >= Options.Bands.HealthyMin);
    }

    [Fact]
    public void Overdue_maintenance_yields_attention_or_worse_with_reason()
    {
        var input = BaseInput() with { MaintenanceStatus = MaintenanceScheduleHelper.StatusOverdue };
        var result = GpsFleetHealthAssessor.Assess(input, Now, Options, 30);
        Assert.Contains("Maintenance overdue", result.Reasons);
        Assert.NotEqual(GpsFleetHealthAssessor.BandOptimal, result.Band);
        Assert.NotEqual(GpsFleetHealthAssessor.BandUnknown, result.Band);
    }

    [Fact]
    public void Critical_alert_open_is_critical_with_reason()
    {
        var input = BaseInput() with { OpenCriticalAlertCount = 1 };
        var result = GpsFleetHealthAssessor.Assess(input, Now, Options, 30);
        Assert.Equal(GpsFleetHealthAssessor.BandCritical, result.Band);
        Assert.Contains(result.Reasons, r => r.Contains("Critical alert", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_device_with_no_other_data_is_unknown()
    {
        var input = new GpsFleetHealthAssessor.VehicleHealthInput(
            2, "No Device", "", HasGpsDevice: false, LastUpdateUtc: null,
            BatteryPercent: null, GsmSignal: null, OpenCriticalAlertCount: 0,
            MaintenanceStatus: "None", InsuranceStatus: "Unknown");
        var result = GpsFleetHealthAssessor.Assess(input, Now, Options, 30);
        Assert.Equal(GpsFleetHealthAssessor.BandUnknown, result.Band);
        Assert.Null(result.Score);
    }

    [Fact]
    public void All_weights_zero_yields_unknown()
    {
        var opts = new GpsFleetHealthOptions
        {
            Weights = new GpsFleetHealthWeights
            {
                GpsFreshness = 0,
                TrackerBattery = 0,
                GsmSignal = 0,
                CriticalAlerts = 0,
                Maintenance = 0,
                Insurance = 0
            }
        };
        var result = GpsFleetHealthAssessor.Assess(BaseInput(), Now, opts, 30);
        Assert.Equal(GpsFleetHealthAssessor.BandUnknown, result.Band);
        Assert.Null(result.Score);
    }

    [Fact]
    public void Stale_gps_reason_is_attention_factor_not_operational_offline()
    {
        var input = BaseInput() with { LastUpdateUtc = Now.AddMinutes(-45) };
        var result = GpsFleetHealthAssessor.Assess(input, Now, Options, 30);
        Assert.Contains("GPS stale", result.Reasons);
        var freshness = result.Factors.Single(f => f.Key == GpsFleetHealthAssessor.FactorGpsFreshness);
        Assert.Equal(GpsFleetHealthAssessor.StateAttention, freshness.State);
    }

    [Fact]
    public void Null_battery_is_unknown_factor_not_optimal()
    {
        var input = BaseInput() with { BatteryPercent = null };
        var result = GpsFleetHealthAssessor.Assess(input, Now, Options, 30);
        var battery = result.Factors.Single(f => f.Key == GpsFleetHealthAssessor.FactorTrackerBattery);
        Assert.Equal(GpsFleetHealthAssessor.StateUnknown, battery.State);
    }

    [Fact]
    public void Summarize_averages_scores_excluding_unknown()
    {
        var good = GpsFleetHealthAssessor.Assess(BaseInput(), Now, Options, 30);
        var unknown = new FleetVehicleHealthDto(
            9, "X", "", GpsFleetHealthAssessor.BandUnknown, null, [], []);
        var summary = GpsFleetHealthAssessor.Summarize([good, unknown]);
        Assert.Equal(1, summary.Assessed);
        Assert.Equal(1, summary.Unknown);
        Assert.Equal(good.Score, summary.AssessedPercent);
    }
}
