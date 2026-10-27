using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GmeetOfflineSosSmsTests
{
    [Theory]
    [InlineData("gmeet_generic", null, null, null, null, null, true)]
    [InlineData("jimi_vg03", "gmeet_generic", null, null, null, null, true)]
    [InlineData(null, null, "Gmeet", null, null, null, true)]
    [InlineData(null, null, null, "Gmeet Electronics", null, null, true)]
    [InlineData(null, null, null, null, "Gmeet X1", null, true)]
    [InlineData(null, null, null, null, null, "Unit Gmeet-01", true)]
    [InlineData("jimi_vg03", null, "Jimi IoT", "Jimi", "VG03", "VG03 truck", false)]
    [InlineData("sinotrack_st901", null, "Sinotrack", null, "ST-901", "ST901", false)]
    public void Matcher_IdentifiesGmeet(
        string? catalogKey,
        string? modelKey,
        string? brand,
        string? vendor,
        string? model,
        string? name,
        bool expected)
    {
        GmeetDeviceMatcher.IsGmeet(catalogKey, modelKey, brand, vendor, model, name)
            .Should().Be(expected);
    }

    [Fact]
    public void Matcher_UsesExtraNameContains()
    {
        GmeetDeviceMatcher.IsGmeet(
                "unknown_x", null, null, null, null, "Fleet box A",
                extraNameContains: ["fleet box"])
            .Should().BeTrue();
    }

    [Fact]
    public void SosResolver_PrefersSosNumber_OverContact()
    {
        GpsDeviceSosNumberResolver.Resolve("03011111111", "03022222222", "03033333333")
            .Should().Be("03011111111");
    }

    [Fact]
    public void SosResolver_FallsBackToPhoneLikeContact()
    {
        GpsDeviceSosNumberResolver.Resolve(null, "+92 301 6169209", "03039999999")
            .Should().Be("+923016169209");
    }

    [Fact]
    public void SosResolver_IgnoresDeviceSim_AndFreeTextContact()
    {
        GpsDeviceSosNumberResolver.Resolve(null, "Ali / vendor desk", "03039999999")
            .Should().BeNull();
    }

    [Fact]
    public void SosResolver_DoesNotInventNumbers()
    {
        GpsDeviceSosNumberResolver.Resolve(null, null, null).Should().BeNull();
        GpsDeviceSosNumberResolver.Resolve("  ", "hi", "").Should().BeNull();
    }

    [Fact]
    public void Mask_HidesLeadingDigits()
    {
        GpsDeviceSosNumberResolver.Mask("03016169209")
            .Should().EndWith("9209")
            .And.StartWith("*");
    }

    [Fact]
    public void CatalogConstants_AreStable()
    {
        GmeetDeviceMatcher.DefaultCatalogKey.Should().Be("gmeet_generic");
        GmeetDeviceMatcher.BrandName.Should().Be("Gmeet");
        GmeetDeviceMatcher.CatalogKeyPrefix.Should().Be("gmeet_");
    }
}
