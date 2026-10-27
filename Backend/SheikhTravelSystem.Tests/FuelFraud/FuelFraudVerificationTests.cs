using SheikhTravelSystem.Application.Features.FuelFraud;
using Xunit;

namespace SheikhTravelSystem.Tests.FuelFraud;

public class FuelFraudVerificationTests
{
    [Fact]
    public void Matched_when_obd_increase_covers_ordered_liters()
    {
        var r = FuelFraudVerification.Evaluate(10m, 40m, 50m, 100m, toleranceLiters: 1.5m);
        Assert.Equal(FuelFraudVerification.StatusMatched, r.Status);
        Assert.Equal(10m, r.ActualIncreaseLiters);
    }

    [Fact]
    public void Fraud_when_shortfall_exceeds_tolerance()
    {
        var r = FuelFraudVerification.Evaluate(10m, 40m, 45m, 100m, toleranceLiters: 1.5m);
        Assert.Equal(FuelFraudVerification.StatusFraudAlert, r.Status);
        Assert.Equal(5m, r.ActualIncreaseLiters);
        Assert.Equal(5m, r.ShortfallLiters);
    }

    [Fact]
    public void NoObd_when_level_missing()
    {
        var r = FuelFraudVerification.Evaluate(10m, null, 50m, 100m);
        Assert.Equal(FuelFraudVerification.StatusNoObd, r.Status);
    }
}
