using SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

namespace SheikhTravelSystem.Tests.AI;

public class McpAllowlistTests
{
    [Theory]
    [InlineData("list_trips")]
    [InlineData("list_fuel_records")]
    [InlineData("get_live_positions")]
    [InlineData("list_smart_alerts")]
    [InlineData("run_readonly_query")]
    [InlineData("db_connection_status")]
    public void Allowlisted_reporting_tools_are_allowed(string tool)
        => Assert.True(McpAllowlist.IsAllowed(tool));

    [Theory]
    [InlineData("scaffold_command")]
    [InlineData("draft_adr")]
    [InlineData("write_architecture_note")]
    [InlineData("architecture_decision")]
    [InlineData("analyze_architecture")]
    [InlineData("search_code")]
    [InlineData("get_write_policy")]
    public void Architecture_and_write_tools_are_denied(string tool)
    {
        Assert.True(McpAllowlist.IsDenied(tool));
        Assert.False(McpAllowlist.IsAllowed(tool));
    }

    [Fact]
    public void BuildSafeArguments_overwrites_client_tenantId()
    {
        var args = SheikhGoMcpBridge.BuildSafeArguments(
            tenantId: 42,
            toolName: "list_trips",
            arguments: new Dictionary<string, object?>
            {
                ["tenantId"] = 999,
                ["status"] = "Completed"
            });

        Assert.Equal(42L, Convert.ToInt64(args["tenantId"]));
        Assert.Equal("Completed", args["status"]);
    }

    [Fact]
    public void BuildSafeArguments_rejects_destructive_sql()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SheikhGoMcpBridge.BuildSafeArguments(
                1,
                "run_readonly_query",
                new Dictionary<string, object?>
                {
                    ["sql"] = "DELETE FROM Bookings WHERE TenantId = 1"
                }));
    }

    [Fact]
    public void BuildSafeArguments_allows_select_with_forced_tenant()
    {
        var args = SheikhGoMcpBridge.BuildSafeArguments(
            7,
            "run_readonly_query",
            new Dictionary<string, object?>
            {
                ["sql"] = "SELECT TOP 10 Id FROM Bookings WHERE TenantId = @TenantId"
            });

        Assert.Equal(7L, Convert.ToInt64(args["tenantId"]));
        Assert.Equal(50, Convert.ToInt32(args["maxRows"]));
    }
}
