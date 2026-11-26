using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Reports.Fleet;
using SheikhTravelSystem.Infrastructure.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>Pakistan (UTC+5) is the platform default; UAE (UTC+4) and others stay configurable per tenant/branch.</summary>
public class TenantTimeZoneTests
{
    private const string Karachi = "Asia/Karachi";
    private const string Dubai = "Asia/Dubai";
    private static TimeZoneInfo Zone(string id) => TenantTimeZoneHelper.Resolve(id, Karachi);

    // 26 Nov 2026 01:30 UTC = 06:30 PKT = 05:30 GST
    private static readonly DateTime Now = new(2026, 11, 26, 1, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Platform_default_is_Karachi_unless_configured()
    {
        new LocalizationOptions().EffectiveDefaultTimeZone.Should().Be(Karachi);
        new LocalizationOptions { DefaultTimeZone = "  " }.EffectiveDefaultTimeZone.Should().Be(Karachi);
        new LocalizationOptions { DefaultTimeZone = Dubai }.EffectiveDefaultTimeZone.Should().Be(Dubai);
    }

    [Theory]
    [InlineData(Karachi, "2026-11-25T19:00:00Z")]
    [InlineData(Dubai, "2026-11-25T20:00:00Z")]
    public void Today_starts_at_local_midnight_not_utc_midnight(string zoneId, string expectedUtc)
    {
        var start = TenantTimeZoneHelper.LocalTodayStartUtc(Zone(zoneId), Now);

        start.Should().Be(DateTime.Parse(expectedUtc).ToUniversalTime());
        start.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Early_morning_trip_belongs_to_the_local_day()
    {
        // 02:15 PKT on 24 Nov is 21:15 UTC on 23 Nov.
        var tripUtc = new DateTime(2026, 11, 23, 21, 15, 0, DateTimeKind.Utc);

        TenantTimeZoneHelper.UtcToLocalDate(tripUtc, Zone(Karachi)).Should().Be(new DateOnly(2026, 11, 24));
        TenantTimeZoneHelper.UtcToLocalDate(tripUtc, Zone(Dubai)).Should().Be(new DateOnly(2026, 11, 24));
        TenantTimeZoneHelper.UtcToLocalDate(tripUtc, TimeZoneInfo.Utc).Should().Be(new DateOnly(2026, 11, 23));
    }

    [Fact]
    public void Day_range_is_half_open_in_utc()
    {
        var (start, end) = TenantTimeZoneHelper.LocalDayRangeUtc(
            new DateOnly(2026, 11, 24), new DateOnly(2026, 11, 24), Zone(Karachi));

        start.Should().Be(new DateTime(2026, 11, 23, 19, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 11, 24, 19, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("today", Karachi, "2026-11-25T19:00:00Z", "2026-11-26T19:00:00Z")]
    [InlineData("yesterday", Karachi, "2026-11-24T19:00:00Z", "2026-11-25T19:00:00Z")]
    [InlineData("last7", Karachi, "2026-11-19T19:00:00Z", "2026-11-26T19:00:00Z")]
    [InlineData("today", Dubai, "2026-11-25T20:00:00Z", "2026-11-26T20:00:00Z")]
    public void Date_presets_use_local_days(string preset, string zoneId, string fromUtc, string toExclusiveUtc)
    {
        var range = TenantTimeZoneHelper.ResolvePresetRangeUtc(preset, Zone(zoneId), Now);

        range.Should().NotBeNull();
        range!.Value.From.Should().Be(DateTime.Parse(fromUtc).ToUniversalTime());
        range.Value.To.Should().Be(DateTime.Parse(toExclusiveUtc).ToUniversalTime().AddTicks(-1));
    }

    [Fact]
    public void Unknown_preset_is_null_and_last24_is_rolling()
    {
        TenantTimeZoneHelper.ResolvePresetRangeUtc("custom", Zone(Karachi), Now).Should().BeNull();
        TenantTimeZoneHelper.ResolvePresetRangeUtc("last24", Zone(Karachi), Now)!.Value.From.Should().Be(Now.AddHours(-24));
    }

    [Fact]
    public void Invalid_or_blank_zone_falls_back_to_default()
    {
        TenantTimeZoneHelper.Resolve("Mars/Olympus", Karachi).BaseUtcOffset.Should().Be(TimeSpan.FromHours(5));
        TenantTimeZoneHelper.Resolve(null, Dubai).BaseUtcOffset.Should().Be(TimeSpan.FromHours(4));
        TenantTimeZoneHelper.IsValid("Mars/Olympus").Should().BeFalse();
        TenantTimeZoneHelper.IsValid(Dubai).Should().BeTrue();
    }

    [Theory]
    [InlineData(Dubai, Karachi, Dubai)]          // branch overrides tenant
    [InlineData(null, Dubai, Dubai)]             // UAE tenant keeps Dubai
    [InlineData("", null, Karachi)]              // nothing set → platform default
    [InlineData("Bad/Zone", Dubai, Dubai)]       // invalid branch zone skipped
    [InlineData(" Asia/Riyadh ", Karachi, "Asia/Riyadh")]
    public void Resolver_order_is_branch_then_tenant_then_default(string? branch, string? tenant, string expected)
        => TenantTimeZoneResolver.Pick(branch, tenant, Karachi).Should().Be(expected);

    [Theory]
    [InlineData(Karachi, "2026-11-27T01:00:00Z")]   // 06:00 PKT next day
    [InlineData(Dubai, "2026-11-27T02:00:00Z")]     // 06:00 GST next day
    public void Daily_report_runs_at_six_local(string zoneId, string expectedUtc)
        => AnalyticsReportHelper.ComputeNextRunAt("daily", Now, Zone(zoneId))
            .Should().Be(DateTime.Parse(expectedUtc).ToUniversalTime());

    [Fact]
    public void Report_schedule_without_zone_keeps_utc_behaviour()
        => AnalyticsReportHelper.ComputeNextRunAt("daily", Now).Should().Be(new DateTime(2026, 11, 27, 6, 0, 0));

    [Fact]
    public void Fleet_report_default_range_ends_on_tenant_today()
    {
        var zone = Zone(Karachi);
        var localToday = TenantTimeZoneHelper.UtcToLocalDate(DateTime.UtcNow, zone).ToDateTime(TimeOnly.MinValue);

        var (from, toExclusive) = FleetReportHelper.ResolveDateRange(null, null, zone);

        toExclusive.Should().Be(localToday.AddDays(1));
        from.Should().Be(new DateTime(localToday.Year, localToday.Month, 1));
    }

    [Theory]
    [InlineData(null, Karachi)]
    [InlineData("Not/AZone", Karachi)]
    [InlineData(Dubai, Dubai)]
    public async Task Branding_exposes_effective_timezone(string? stored, string expected)
    {
        var tenants = new Moq.Mock<SheikhTravelSystem.Application.Common.Interfaces.Repositories.ITenantRepository>();
        tenants.Setup(r => r.GetBrandingAsync(7, Moq.It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SheikhTravelSystem.Application.Common.Interfaces.Repositories.TenantBrandingRow(7, "Demo", "demo", stored));
        var tenantContext = new Moq.Mock<SheikhTravelSystem.Application.Common.Interfaces.ITenantContext>();
        tenantContext.Setup(t => t.GetRequiredTenantId()).Returns(7);
        var modules = new Moq.Mock<SheikhTravelSystem.Application.Common.Interfaces.ITenantModuleService>();
        modules.Setup(m => m.GetLegacyModuleKeysAsync(7, Moq.It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var resolver = new Moq.Mock<SheikhTravelSystem.Application.Common.Interfaces.ITenantTimeZoneResolver>();
        resolver.SetupGet(r => r.DefaultTimeZoneId).Returns(Karachi);
        var handler = new SheikhTravelSystem.Application.Features.Tenants.Queries.GetTenantBrandingQueryHandler(
            tenants.Object, tenantContext.Object, modules.Object, resolver.Object);

        var result = await handler.Handle(new SheikhTravelSystem.Application.Features.Tenants.Queries.GetTenantBrandingQuery(), CancellationToken.None);

        result.Data!.TimeZone.Should().Be(stored);
        result.Data.EffectiveTimeZone.Should().Be(expected);
    }

    [Fact]
    public void Fleet_report_explicit_dates_are_unchanged_by_zone()
    {
        var from = new DateTime(2026, 11, 1);
        var to = new DateTime(2026, 11, 24);

        FleetReportHelper.ResolveDateRange(from, to, Zone(Dubai))
            .Should().Be(FleetReportHelper.ResolveDateRange(from, to));
    }
}
