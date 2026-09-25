using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsFleetStatusBucketsTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Speed_0_ignition_off_fresh_is_parked()
    {
        var bucket = GpsFleetStatusBuckets.Classify(
            hasGpsDevice: true,
            lastUpdateUtc: Now.AddMinutes(-1),
            speedKmh: 0,
            ignition: false,
            alarmType: null,
            utcNow: Now);

        Assert.Equal("parked", bucket);
    }

    [Fact]
    public void Speed_35_ignition_on_fresh_is_moving()
    {
        var bucket = GpsFleetStatusBuckets.Classify(
            hasGpsDevice: true,
            lastUpdateUtc: Now.AddMinutes(-1),
            speedKmh: 35,
            ignition: true,
            alarmType: null,
            utcNow: Now);

        Assert.Equal("moving", bucket);
    }

    [Fact]
    public void Speed_0_ignition_on_fresh_is_idle()
    {
        var bucket = GpsFleetStatusBuckets.Classify(
            hasGpsDevice: true,
            lastUpdateUtc: Now.AddMinutes(-1),
            speedKmh: 0,
            ignition: true,
            alarmType: null,
            utcNow: Now);

        Assert.Equal("idle", bucket);
    }

    [Fact]
    public void Old_gps_is_offline()
    {
        var bucket = GpsFleetStatusBuckets.Classify(
            hasGpsDevice: true,
            lastUpdateUtc: Now.AddMinutes(-45),
            speedKmh: 0,
            ignition: false,
            alarmType: null,
            utcNow: Now);

        Assert.Equal("offline", bucket);
    }

    [Fact]
    public void Device_never_reported_is_never_seen()
    {
        var bucket = GpsFleetStatusBuckets.Classify(
            hasGpsDevice: true,
            lastUpdateUtc: null,
            speedKmh: null,
            ignition: null,
            alarmType: null,
            utcNow: Now);

        Assert.Equal("never_seen", bucket);
    }

    [Fact]
    public void Ignition_off_low_drift_is_parked()
    {
        var bucket = GpsFleetStatusBuckets.Classify(
            hasGpsDevice: true,
            lastUpdateUtc: Now.AddMinutes(-1),
            speedKmh: 4,
            ignition: false,
            alarmType: null,
            utcNow: Now);

        Assert.Equal("parked", bucket);
    }

    [Fact]
    public void Ignition_off_high_speed_is_unknown()
    {
        var bucket = GpsFleetStatusBuckets.Classify(
            hasGpsDevice: true,
            lastUpdateUtc: Now.AddMinutes(-1),
            speedKmh: 42,
            ignition: false,
            alarmType: null,
            utcNow: Now);

        Assert.Equal("unknown", bucket);
    }

    [Fact]
    public void Ignition_null_low_speed_is_unknown()
    {
        var bucket = GpsFleetStatusBuckets.Classify(
            hasGpsDevice: true,
            lastUpdateUtc: Now.AddMinutes(-1),
            speedKmh: 0,
            ignition: null,
            alarmType: null,
            utcNow: Now);

        Assert.Equal("unknown", bucket);
    }

    [Fact]
    public void Sos_alarm_wins_when_online()
    {
        var bucket = GpsFleetStatusBuckets.Classify(
            hasGpsDevice: true,
            lastUpdateUtc: Now.AddMinutes(-1),
            speedKmh: 0,
            ignition: false,
            alarmType: "panic",
            utcNow: Now);

        Assert.Equal("sos", bucket);
    }

    [Fact]
    public void Stale_update_is_offline_even_with_sos_alarm()
    {
        var bucket = GpsFleetStatusBuckets.Classify(
            hasGpsDevice: true,
            lastUpdateUtc: Now.AddMinutes(-45),
            speedKmh: 0,
            ignition: false,
            alarmType: "sos",
            utcNow: Now);

        Assert.Equal("offline", bucket);
    }
}
