using System.Text.Json;
using FluentAssertions;
using SheikhTravelSystem.Infrastructure.Services.Ai;
using SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

namespace SheikhTravelSystem.Tests.AI;

public class FleetStatusAnalyticsEnricherTests
{
    private const string RawFleetStatus = """
        {
          "source": "live",
          "readOnly": true,
          "tenantId": 1,
          "offlineStaleMinutes": 30,
          "movingThresholdKmh": 10,
          "summary": {
            "total": 3,
            "moving": 0,
            "idle": 0,
            "parked": 1,
            "sos": 0,
            "neverSeen": 0,
            "offline": 2,
            "unknown": 0,
            "online": 1
          },
          "sample": [
            {
              "VehicleId": 2,
              "VehicleName": "Toyota Corolla",
              "RegistrationNumber": "LEB-8825",
              "GpsBucket": "parked",
              "Speed": 0,
              "Ignition": false,
              "LastUpdate": "2026-10-28T08:59:50.570Z",
              "VehicleStatusName": "Available"
            },
            {
              "VehicleId": 2003,
              "VehicleName": "BYD e6 Shuttle",
              "RegistrationNumber": "EV-DEMO-001",
              "GpsBucket": "offline",
              "Speed": 2,
              "LastUpdate": "2026-10-27T16:39:50.633Z",
              "VehicleStatusName": "Available"
            }
          ],
          "warnings": [],
          "note": "Real-time buckets from VehicleCurrentLocation + GpsDevices."
        }
        """;

    [Fact]
    public void TryEnrich_builds_fleet_status_analytics_from_raw_mcp()
    {
        var json = FleetStatusAnalyticsEnricher.TryEnrich(RawFleetStatus);
        json.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(json!);
        var root = doc.RootElement;
        root.GetProperty("responseType").GetString().Should().Be("fleet_status_analytics");
        root.GetProperty("kpis").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("statusDistribution").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("vehicles").GetArrayLength().Should().Be(2);
        root.GetProperty("insights").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("recommendedActions").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("evidence").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("followUps").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("answer").GetString().Should().Contain("online");
        root.GetProperty("context").GetString().Should().Contain("VehicleCurrentLocation");
        root.GetProperty("interpretation").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("nextAction").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryExtractStructuredAnalytics_captures_enriched_fleet_status()
    {
        var ok = AiMcpToolCallingLoop.TryExtractStructuredAnalytics(RawFleetStatus, out var structured);
        ok.Should().BeTrue();
        structured.Should().NotBeNull();

        var reparsed = JsonSerializer.Serialize(structured);
        using var doc = JsonDocument.Parse(reparsed);
        doc.RootElement.GetProperty("responseType").GetString().Should().Be("fleet_status_analytics");
    }

    [Fact]
    public void TryEnrich_returns_null_for_unrelated_payload()
    {
        FleetStatusAnalyticsEnricher.TryEnrich("""{"rows":[{"id":1}]}""").Should().BeNull();
    }
}
