using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using Xunit;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsGt06CommandMapTests
{
    [Theory]
    [InlineData("restart", "RESET#", "custom", null)]
    [InlineData("positionSingle", "WHERE#", "custom", "jimi_ev26r")]
    [InlineData("positionSingle", "URL#", "custom", "jimi_vg03")]
    [InlineData("engineStop", "RELAY,1#", "engineStop", null)]
    [InlineData("engineResume", "RELAY,0#", "engineResume", null)]
    [InlineData("status", "STATUS#", "custom", null)]
    [InlineData("check", "CHECK#", "custom", null)]
    [InlineData("iccid", "ICCID#", "custom", null)]
    [InlineData("imsi", "IMSI#", "custom", null)]
    [InlineData("apn", "APN#", "custom", null)]
    [InlineData("gprsset", "GPRSSET#", "custom", null)]
    [InlineData("server", "GPRSSET#", "custom", null)]
    public void TryMap_ReturnsModelAwarePayloads(string key, string payload, string traccarType, string? catalog)
    {
        var mapped = GpsGt06CommandMap.TryMap(key, catalog);
        mapped.Should().NotBeNull();
        mapped!.Payload.Should().Be(payload);
        mapped.TraccarType.Should().Be(traccarType);
    }

    [Fact]
    public void TryMap_DoesNotUseRebootDevice()
    {
        var mapped = GpsGt06CommandMap.TryMap("restart");
        mapped!.TraccarType.Should().NotBe("rebootDevice");
    }

    [Fact]
    public void Vg03Panel_ExcludesEv26rOnlyDiagnostics()
    {
        GpsGt06CommandMap.Vg03PanelCommandKeys.Should().Contain(new[]
        {
            "engineStop", "engineResume", "positionSingle", "restart", "status", "gprsset", "server"
        });
        GpsGt06CommandMap.Vg03PanelCommandKeys.Should().NotContain(new[]
        {
            "check", "iccid", "imsi", "apn", "param", "buzzer"
        });
    }

    [Fact]
    public void Ev26rPanel_IncludesFullDiagnostics()
    {
        GpsGt06CommandMap.Ev26rPanelCommandKeys.Should().Contain(new[]
        {
            "engineStop", "restart", "status", "check", "iccid", "imsi", "apn", "gprsset", "server"
        });
    }

    [Fact]
    public void PanelKeysForCatalog_SplitsVg03AndEv26r()
    {
        GpsGt06CommandMap.PanelKeysForCatalog("jimi_vg03")
            .Should().BeEquivalentTo(GpsGt06CommandMap.Vg03PanelCommandKeys);
        GpsGt06CommandMap.PanelKeysForCatalog("jimi_ev26r")
            .Should().BeEquivalentTo(GpsGt06CommandMap.Ev26rPanelCommandKeys);
    }

    [Theory]
    [InlineData("gt06", null, null, null, null, true)]
    [InlineData("jimi", "VG03", "Jimi", "jimi_vg03", "Jimi VG03", true)]
    [InlineData("teltonika", "FMB920", "Teltonika", "teltonika_fmb920", "Unit 1", false)]
    public void LooksLikeGt06Family_DetectsProtocol(
        string? protocol, string? model, string? vendor, string? key, string? name, bool expected)
    {
        GpsGt06CommandMap.LooksLikeGt06Family(protocol, model, vendor, key, name)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("jimi_vg03", null, null, true)]
    [InlineData(null, "JM-VG03", "Fleet VG03", true)]
    [InlineData("jimi_ev26r", "EV26R", "EV26R-1", false)]
    public void LooksLikeVg03_DetectsModel(string? catalog, string? model, string? name, bool expected)
    {
        GpsGt06CommandMap.LooksLikeVg03(catalog, model, name).Should().Be(expected);
    }
}
