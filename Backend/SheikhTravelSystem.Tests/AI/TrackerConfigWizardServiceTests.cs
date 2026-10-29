using System.Text.Json;
using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;
using SheikhTravelSystem.Infrastructure.Services.Ai.Tools;
using Xunit;

namespace SheikhTravelSystem.Tests.AI;

/// <summary>Pure helpers / catalog coverage for the ConfigureTracker wizard (no DB).</summary>
public sealed class TrackerConfigWizardServiceTests
{
    [Theory]
    [InlineData("jimi_gt06", true)]
    [InlineData("Jimi GT06", true)]
    [InlineData("teltonika_fmb920", true)]
    [InlineData("not-a-model", false)]
    public void TrackerCatalog_resolves_known_models(string input, bool expectHit)
    {
        var key = input.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        TrackerModelInfo? byKey = TrackerCatalog.Resolve(key);
        TrackerModelInfo? byLabel = null;
        foreach (var (k, info) in TrackerCatalog.Models)
        {
            if (info.Label.Equals(input.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                byLabel = info;
                break;
            }
        }

        var hit = byKey is not null || byLabel is not null;
        hit.Should().Be(expectHit);
    }

    [Fact]
    public void Wizard_state_json_round_trips_draft_fields_for_confirm_tool()
    {
        var state = new
        {
            phase = 1, // Ready
            step = 12, // Done
            draft = new
            {
                uniqueId = "123456789012345",
                name = "Truck-12 GPS",
                category = "truck",
                trackerModelKey = "jimi_gt06",
                countryCode = "+92",
                phone = "3001234567",
                contact = "Ali",
                sosNumber = "1122",
                simProvider = "Jazz",
                serialNumber = "SN-1",
                vendor = "Local",
                supportsEngineCutoff = true,
                relayOutput = "out1"
            }
        };

        var json = JsonSerializer.Serialize(state);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("draft", out var draft).Should().BeTrue();
        draft.GetProperty("uniqueId").GetString().Should().Be("123456789012345");
        draft.GetProperty("trackerModelKey").GetString().Should().Be("jimi_gt06");
        draft.GetProperty("supportsEngineCutoff").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void ConfigureTracker_tool_metadata_is_write_with_confirmation()
    {
        var tool = new ConfigureTrackerTool(
            mediator: null!,
            logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigureTrackerTool>.Instance);

        tool.Name.Should().Be(TrackerConfigWizardService.ToolName);
        tool.Kind.Should().Be("write");
        tool.RequiresConfirmation.Should().BeTrue();
        tool.TriggerKeywords.Should().Contain(k => k.Contains("register tracker"));
    }
}
