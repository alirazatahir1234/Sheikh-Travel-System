using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsCommandTemplateRendererTests
{
    [Fact]
    public void Render_EngineStop_Ev26rRelayTemplate_ProducesRelayOnPayload()
    {
        // EV26R engine immobilizer SMS form (also used when Traccar custom data carries the same string)
        var rendered = GpsCommandTemplateRenderer.Render("RELAY,1#", null);
        rendered.Should().Be("RELAY,1#");
    }

    [Fact]
    public void Render_SubstitutesPlaceholders()
    {
        var rendered = GpsCommandTemplateRenderer.Render(
            "HBT,{{intervalSeconds}},{{intervalSeconds}}#",
            new Dictionary<string, string> { ["intervalSeconds"] = "120" });
        rendered.Should().Be("HBT,120,120#");
    }

    [Fact]
    public void ResolveBestTemplate_PrefersFirmwareRangeMatch()
    {
        var templates = new[]
        {
            new Tmpl(1, null, null, 1),
            new Tmpl(2, "1.0", "1.9", 2),
            new Tmpl(3, "2.0", "9.9", 3),
        };

        var best = GpsCommandTemplateRenderer.ResolveBestTemplate(
            templates, "2.1", t => t.Min, t => t.Max, t => t.Version);

        best!.Id.Should().Be(3);
    }

    [Fact]
    public void ResolveBestTemplate_FallsBackToNullRange()
    {
        var templates = new[]
        {
            new Tmpl(1, null, null, 5),
            new Tmpl(2, "9.0", "9.9", 9),
        };

        var best = GpsCommandTemplateRenderer.ResolveBestTemplate(
            templates, "1.0", t => t.Min, t => t.Max, t => t.Version);

        best!.Id.Should().Be(1);
    }

    private sealed record Tmpl(int Id, string? Min, string? Max, int Version);
}
