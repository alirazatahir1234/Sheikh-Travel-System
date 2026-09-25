using FluentAssertions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppAutomationPolicyTests
{
    [Fact]
    public void DedupeKey_IncludesTypeAndIds()
    {
        var key = AutomationPolicy.DedupeKey(WaAutomationEventType.DriverArrived, 42);
        key.Should().Be("DriverArrived:42");
    }

    [Fact]
    public void QuietHours_Urgent_Unchanged()
    {
        var tz = TimeZoneInfo.Utc;
        var due = new DateTime(2026, 9, 25, 23, 0, 0, DateTimeKind.Utc);
        AutomationPolicy.ApplyQuietHours(due, tz, isUrgent: true).Should().Be(due);
    }

    [Fact]
    public void QuietHours_NonUrgent_PushesToMorning()
    {
        var tz = TimeZoneInfo.Utc;
        var due = new DateTime(2026, 9, 25, 23, 30, 0, DateTimeKind.Utc);
        var adjusted = AutomationPolicy.ApplyQuietHours(due, tz, isUrgent: false);
        adjusted.Should().Be(new DateTime(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ArrivalStale_IsTenMinutes()
        => AutomationPolicy.ArrivalStaleAfter.Should().Be(TimeSpan.FromMinutes(10));
}

public class WhatsAppGeoMathTests
{
    [Fact]
    public void Haversine_SamePoint_IsZero()
        => GeoMath.DistanceMeters(31.52, 74.35, 31.52, 74.35).Should().BeApproximately(0, 0.01);

    [Fact]
    public void Haversine_KnownShortDistance()
    {
        // ~1.11 km north of Lahore-ish coords
        var d = GeoMath.DistanceMeters(31.5200, 74.3580, 31.5300, 74.3580);
        d.Should().BeInRange(1000, 1300);
    }

    [Fact]
    public void KnotsToKmh()
        => GeoMath.KnotsToKmh(10).Should().BeApproximately(18.52, 0.001);

    [Fact]
    public void Eta_ClampsBetween1And180()
    {
        GeoMath.EstimateEtaMinutes(1).Should().Be(1);
        GeoMath.EstimateEtaMinutes(1_000_000).Should().Be(180);
    }

    [Fact]
    public void Proximity_Arriving_Under800m()
    {
        ProximityRules.IsArriving(799).Should().BeTrue();
        ProximityRules.IsArriving(800).Should().BeFalse();
    }

    [Fact]
    public void Proximity_Arrived_RequiresSlowSpeed()
    {
        ProximityRules.IsArrived(50, 4).Should().BeTrue();
        ProximityRules.IsArrived(50, 10).Should().BeFalse();
        ProximityRules.IsArrived(150, 1).Should().BeFalse();
    }
}

public class WhatsAppTrackingTokenTests
{
    [Fact]
    public void Generate_IsUrlSafeAndHashedLookupStable()
    {
        var token = TrackingToken.Generate();
        token.Should().NotBeNullOrWhiteSpace();
        token.Should().NotContain("+");
        token.Should().NotContain("/");
        TrackingToken.Hash(token).Should().HaveCount(32);
        TrackingToken.Hash(token).Should().Equal(TrackingToken.Hash(token));
    }
}

public class WhatsAppAutomationComposerTests
{
    [Fact]
    public void Compose_TripCompleted_IncludesRatePayloads()
    {
        var composer = new AutomationMessageComposer();
        var trip = new AutomationTripSnapshot(
            99, "T-1", 5, 1, 2, "Ali", 3, "Hiace", "LEC-1",
            31.5, 74.3, "Pickup", null, null, null, DateTime.UtcNow, 10, 5000, "PKR", 0);
        var booking = new AutomationBookingSnapshot(
            1, "B-1", 2, DateTime.UtcNow, "Pickup", 10, "Cust", "+923001234567", "en",
            2, "Ali", 3, "Hiace", "LEC-1", null);

        var composed = composer.Compose(
            WaAutomationEventType.TripCompleted, booking, trip, "en", null, "trip_completed");

        composed.Should().NotBeNull();
        composed!.QuickReplyPayloads.Should().Contain("RATE:99:5");
        composed.QuickReplyPayloads.Should().Contain("RATE:99:1");
    }

    [Fact]
    public void ResolveRecipient_PrefersPassengerPhone_ViaCustomerFallback()
    {
        var booking = new AutomationBookingSnapshot(
            1, "B-1", 2, DateTime.UtcNow, "Pickup", 10, "Cust", "+923001111111", "ur",
            null, null, null, null, null, null);
        AutomationMessageComposer.ResolveRecipient(booking, null).Should().Be("+923001111111");
        AutomationMessageComposer.ResolveLanguage(booking, "en").Should().Be("ur");
    }
}
