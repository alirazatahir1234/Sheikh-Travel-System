using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

namespace SheikhTravelSystem.Tests.AI;

public class McpDatabaseRecordRouterTests
{
    [Theory]
    [InlineData("List vehicles for my company", "list_vehicles")]
    [InlineData("Show me all drivers", "list_drivers")]
    [InlineData("Show bookings please", "list_bookings")]
    [InlineData("List trips", "list_trips")]
    [InlineData("What subscription modules are enabled?", "run_readonly_query")]
    [InlineData("List tables in the database", "list_tables")]
    [InlineData("Describe table Vehicles", "describe_table")]
    [InlineData("Show me records from Drivers", "run_readonly_query")]
    [InlineData("Live GPS positions", "get_live_positions")]
    [InlineData("Fleet status overview", "get_fleet_status")]
    [InlineData("How many vehicles do we have?", "get_vehicle_count")]
    [InlineData("Fuel expenses this month", "get_fuel_expenses")]
    [InlineData("Trip summary please", "get_trip_summary")]
    [InlineData("Booking summary for fleet", "get_booking_summary")]
    [InlineData("Show revenue totals", "get_revenue_summary")]
    [InlineData("Which vehicles are offline?", "get_fleet_status")]
    [InlineData("What is our GPS coverage?", "get_gps_coverage_summary")]
    [InlineData("How many active vehicles?", "get_fleet_inventory_summary")]
    [InlineData("Revenue by branch this month", "get_revenue_breakdown")]
    [InlineData("Week over week comparison", "get_period_comparison")]
    [InlineData("Maintenance KPI overdue counts", "get_maintenance_kpi_summary")]
    public void ResolveIntents_maps_record_questions(string question, string expectedTool)
    {
        var intents = McpDatabaseRecordRouter.ResolveIntents(question.ToLowerInvariant());
        intents.Should().NotBeEmpty();
        intents[0].ToolName.Should().Be(expectedTool);
    }

    [Fact]
    public void LooksLikeRecordQuery_false_for_unrelated_chat()
    {
        var bridge = new Mock<ISheikhGoMcpBridge>();
        bridge.SetupGet(b => b.IsEnabled).Returns(true);
        var router = new McpDatabaseRecordRouter(bridge.Object, NullLogger<McpDatabaseRecordRouter>.Instance);

        router.LooksLikeRecordQuery("hello how are you?").Should().BeFalse();
        router.LooksLikeRecordQuery("List vehicles now").Should().BeTrue();
    }

    [Fact]
    public async Task TryFetchAsync_returns_null_when_bridge_disabled()
    {
        var bridge = new Mock<ISheikhGoMcpBridge>();
        bridge.SetupGet(b => b.IsEnabled).Returns(false);
        var router = new McpDatabaseRecordRouter(bridge.Object, NullLogger<McpDatabaseRecordRouter>.Instance);

        var result = await router.TryFetchAsync(1, "List vehicles", CancellationToken.None);
        result.Should().BeNull();
        bridge.Verify(
            b => b.CallAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryFetchAsync_calls_allowlisted_mcp_tool()
    {
        var bridge = new Mock<ISheikhGoMcpBridge>();
        bridge.SetupGet(b => b.IsEnabled).Returns(true);
        bridge.Setup(b => b.CallAsync(
                7,
                "list_vehicles",
                It.IsAny<IReadOnlyDictionary<string, object?>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolCallResult(true, "list_vehicles", """{"rows":[{"Id":1}]}"""));

        var router = new McpDatabaseRecordRouter(bridge.Object, NullLogger<McpDatabaseRecordRouter>.Instance);
        var result = await router.TryFetchAsync(7, "List vehicles please", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.ToolsUsed.Should().Contain("mcp:list_vehicles");
        result.Summary.Should().Contain("list_vehicles");
    }

    [Fact]
    public void Architecture_list_modules_is_not_routed_as_subscription()
    {
        // "list_modules" architecture phrasing without subscription keywords should not map to TenantModules SQL
        var intents = McpDatabaseRecordRouter.ResolveIntents("analyze list_modules architecture coverage");
        intents.Should().BeEmpty();
    }

    [Fact]
    public void Allowlist_still_denies_architecture_list_modules()
    {
        McpAllowlist.IsDenied("list_modules").Should().BeTrue();
        McpAllowlist.IsAllowed("run_readonly_query").Should().BeTrue();
        McpAllowlist.IsAllowed("list_vehicles").Should().BeTrue();
    }
}
