using SheikhTravelSystem.Application.Features.FatigueDetection;
using Xunit;

namespace SheikhTravelSystem.Tests.FatigueDetection;

public class FatigueRiskScoringTests
{
    [Fact]
    public void Composite_boosts_when_obd_shows_harsh_patterns()
    {
        var calm = FatigueRiskScoring.ComputeComposite(0.7m, 0.4m, null);
        var harsh = FatigueRiskScoring.ComputeComposite(
            0.7m,
            0.4m,
            new FatigueObdSupportingSignals(SpeedKmh: 110, HardBrakingEvents15Min: 4, ProlongedHighSpeed: true));

        Assert.True(harsh > calm);
        Assert.InRange(harsh, 0m, 1m);
    }
}
