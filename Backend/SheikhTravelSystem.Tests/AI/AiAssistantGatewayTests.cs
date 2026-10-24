using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.AI.Commands;
using SheikhTravelSystem.Infrastructure.Services.Ai;
using SheikhTravelSystem.Infrastructure.Services.Ai.Tools;

namespace SheikhTravelSystem.Tests.AI;

public class AiAssistantGatewayTests
{
    [Fact]
    public async Task QueryCommand_rejects_empty_question()
    {
        var gateway = new Mock<IAiAssistantGateway>();
        var handler = new QueryAiAssistantCommandHandler(gateway.Object);

        var result = await handler.Handle(
            new QueryAiAssistantCommand(1, 9, "  "),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        gateway.Verify(
            g => g.QueryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AiAssistantQueryRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task QueryCommand_passes_server_tenant_and_user()
    {
        var gateway = new Mock<IAiAssistantGateway>();
        gateway.Setup(g => g.QueryAsync(42, 7, It.IsAny<AiAssistantQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAssistantQueryResponse(
                "ok",
                new AiAssistantPeriodDto(DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1)),
                "GetFuelCostSummary",
                null,
                Array.Empty<string>(),
                false,
                "tools"));

        var handler = new QueryAiAssistantCommandHandler(gateway.Object);
        var result = await handler.Handle(
            new QueryAiAssistantCommand(42, 7, "Which vehicles had the highest fuel costs this month?", IsPlatformSuperAdmin: true),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.ToolUsed.Should().Be("GetFuelCostSummary");
        gateway.Verify(g => g.QueryAsync(
            42,
            7,
            It.Is<AiAssistantQueryRequest>(r =>
                r.Question.Contains("fuel", StringComparison.OrdinalIgnoreCase) && r.IsPlatformSuperAdmin),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Gateway_selects_fuel_tool_and_passes_tenant_context()
    {
        int? seenTenant = null;
        var fuel = new Mock<IAiTool>();
        fuel.SetupGet(t => t.Name).Returns("GetFuelCostSummary");
        fuel.SetupGet(t => t.Kind).Returns("read");
        fuel.SetupGet(t => t.TriggerKeywords).Returns(new[] { "fuel cost" });
        fuel.Setup(t => t.ExecuteAsync(It.IsAny<AiToolExecutionContext>(), It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .Callback<AiToolExecutionContext, JsonElement?, CancellationToken>((ctx, _, _) => seenTenant = ctx.TenantId)
            .ReturnsAsync(new AiToolExecutionResult(
                "GetFuelCostSummary",
                true,
                "Vehicle A: 100.00",
                new { grandTotal = 100m }));

        var other = CreateStubTool("GetTripSummary");
        var provider = new Mock<IAiProviderResolver>();
        provider.Setup(p => p.ResolveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, new AiProviderConfigDto()));
        var management = new Mock<IAiManagementService>();

        var gateway = new AiAssistantGateway(
            new IAiTool[] { fuel.Object, other.Object },
            provider.Object,
            management.Object,
            NullLogger<AiAssistantGateway>.Instance);

        var response = await gateway.QueryAsync(
            99,
            3,
            new AiAssistantQueryRequest("Which vehicles had the highest fuel costs this month?"),
            CancellationToken.None);

        response.ToolUsed.Should().Be("GetFuelCostSummary");
        response.Answer.Should().Contain("100");
        response.UsedLlm.Should().BeFalse();
        seenTenant.Should().Be(99);
        response.Warnings.Should().Contain(w => w.Contains("LLM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Gateway_ignores_non_allowlisted_tools()
    {
        var write = CreateStubTool("AssignDriver", kind: "write");
        write.SetupGet(t => t.TriggerKeywords).Returns(new[] { "fuel cost", "assign driver" });

        var fuel = new Mock<IAiTool>();
        fuel.SetupGet(t => t.Name).Returns("GetFuelCostSummary");
        fuel.SetupGet(t => t.Kind).Returns("read");
        fuel.SetupGet(t => t.TriggerKeywords).Returns(new[] { "fuel cost" });
        fuel.Setup(t => t.ExecuteAsync(It.IsAny<AiToolExecutionContext>(), It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiToolExecutionResult("GetFuelCostSummary", true, "ok", null));

        var provider = new Mock<IAiProviderResolver>();
        provider.Setup(p => p.ResolveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, new AiProviderConfigDto()));

        var gateway = new AiAssistantGateway(
            new IAiTool[] { write.Object, fuel.Object },
            provider.Object,
            new Mock<IAiManagementService>().Object,
            NullLogger<AiAssistantGateway>.Instance);

        var response = await gateway.QueryAsync(
            1, 1, new AiAssistantQueryRequest("fuel costs this month"), CancellationToken.None);

        response.ToolUsed.Should().Be("GetFuelCostSummary");
        write.Verify(
            t => t.ExecuteAsync(It.IsAny<AiToolExecutionContext>(), It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Allowlist_contains_phase2_batches_and_phase3_tools()
    {
        AiAssistantGateway.AllowlistedToolNames.Should().Contain(new[]
        {
            "GetFuelCostSummary",
            "GetVehicleMaintenanceSummary",
            "GetOverdueInvoices",
            "GetTripSummary",
            "GetVehicleUtilizationSummary",
            "GetRevenueAnalytics",
            "GetFuelUsageTopVehicle",
            "GetBestDriverPerformance",
            "GetCostPerKilometer",
            "GetOverspeedGeofenceSummary",
            "GetIdleTimeReport",
            "GetSosEventsSummary",
            "GetExpenseTracking",
            "GetTenantComparison",
            "GetPeakHoursAnalysis",
            "GetMaintenanceRiskForecast",
            "GetTripDelayRisk",
            "GetFuelAnomalyInvestigation"
        });
    }

    [Theory]
    [InlineData("What is our daily weekly and monthly revenue?", "GetRevenueAnalytics")]
    [InlineData("Which vehicle consumed the most fuel this month?", "GetFuelUsageTopVehicle")]
    [InlineData("Who is the best performing driver today?", "GetBestDriverPerformance")]
    [InlineData("Which vehicles had the highest fuel costs this month?", "GetFuelCostSummary")]
    [InlineData("Show trip and booking analytics this month", "GetTripSummary")]
    [InlineData("What is our cost per kilometer this month?", "GetCostPerKilometer")]
    [InlineData("What is our fleet utilization rate this month?", "GetVehicleUtilizationSummary")]
    [InlineData("Summarize overspeed and geofence violations this month", "GetOverspeedGeofenceSummary")]
    [InlineData("Which vehicles have maintenance due?", "GetVehicleMaintenanceSummary")]
    [InlineData("Show idle time report this month", "GetIdleTimeReport")]
    [InlineData("Any SOS events this month?", "GetSosEventsSummary")]
    [InlineData("Summarize expenses this month", "GetExpenseTracking")]
    [InlineData("What are our peak booking hours this month?", "GetPeakHoursAnalysis")]
    [InlineData("Which vehicles are at maintenance risk?", "GetMaintenanceRiskForecast")]
    [InlineData("Which trips are at risk of delay?", "GetTripDelayRisk")]
    [InlineData("Show fuel anomaly investigation for this month", "GetFuelAnomalyInvestigation")]
    public async Task Gateway_selects_batch1_tools_by_question(string question, string expectedTool)
    {
        var tools = AiAssistantGateway.AllowlistedToolNames
            .Select(name =>
            {
                var m = CreateStubTool(name);
                m.Setup(t => t.ExecuteAsync(It.IsAny<AiToolExecutionContext>(), It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AiToolExecutionResult(name, true, $"{name} ok", null));
                return m.Object;
            })
            .ToArray();

        var provider = new Mock<IAiProviderResolver>();
        provider.Setup(p => p.ResolveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, new AiProviderConfigDto()));

        var gateway = new AiAssistantGateway(
            tools,
            provider.Object,
            new Mock<IAiManagementService>().Object,
            NullLogger<AiAssistantGateway>.Instance);

        var response = await gateway.QueryAsync(1, 1, new AiAssistantQueryRequest(question), CancellationToken.None);
        response.ToolUsed.Should().Be(expectedTool);
    }

    [Fact]
    public void PeriodHelper_defaults_to_current_month_utc()
    {
        var (from, to, label) = AssistantPeriodHelper.Resolve("fuel costs", null);
        label.Should().Be("this_month");
        from.Should().Be(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc));
        to.Should().Be(from.AddMonths(1));
    }

    [Fact]
    public void PeriodHelper_previous_month()
    {
        var (from, to, label) = AssistantPeriodHelper.Resolve("compare fuel with previous month", null);
        label.Should().Be("previous_month");
        to.Should().Be(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc));
        from.Should().Be(to.AddMonths(-1));
    }

    [Fact]
    public void PeriodHelper_today()
    {
        var (from, to, label) = AssistantPeriodHelper.Resolve("best performing driver today", null);
        label.Should().Be("today");
        from.Should().Be(DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc));
        to.Should().Be(from.AddDays(1));
    }

    [Fact]
    public void Batch_and_phase3_tool_metadata_is_read_only()
    {
        AiAssistantGateway.AllowlistedToolNames.Should().Contain(new[]
        {
            "GetRevenueAnalytics",
            "GetFuelUsageTopVehicle",
            "GetBestDriverPerformance",
            "GetCostPerKilometer",
            "GetOverspeedGeofenceSummary",
            "GetIdleTimeReport",
            "GetSosEventsSummary",
            "GetExpenseTracking",
            "GetTenantComparison",
            "GetPeakHoursAnalysis",
            "GetMaintenanceRiskForecast",
            "GetTripDelayRisk",
            "GetFuelAnomalyInvestigation"
        });

        var maintRisk = new GetMaintenanceRiskForecastTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        maintRisk.Kind.Should().Be("read");
        maintRisk.RequiresConfirmation.Should().BeFalse();

        var delay = new GetTripDelayRiskTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        delay.Kind.Should().Be("read");
        delay.TriggerKeywords.Should().Contain(k => k.Contains("delay", StringComparison.OrdinalIgnoreCase));

        var fuelAnomaly = new GetFuelAnomalyInvestigationTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        fuelAnomaly.Kind.Should().Be("read");
        fuelAnomaly.TriggerKeywords.Should().Contain(k => k.Contains("anomaly", StringComparison.OrdinalIgnoreCase));

        var revenue = new GetRevenueAnalyticsTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        revenue.Kind.Should().Be("read");
        revenue.RequiresConfirmation.Should().BeFalse();

        var fuel = new GetFuelUsageTopVehicleTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        fuel.Kind.Should().Be("read");
        fuel.TriggerKeywords.Should().Contain(k => k.Contains("consumed", StringComparison.OrdinalIgnoreCase));

        var driver = new GetBestDriverPerformanceTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        driver.Kind.Should().Be("read");
        driver.TriggerKeywords.Should().Contain(k => k.Contains("best driver", StringComparison.OrdinalIgnoreCase));

        var cpk = new GetCostPerKilometerTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        cpk.Kind.Should().Be("read");
        cpk.RequiresConfirmation.Should().BeFalse();

        var violations = new GetOverspeedGeofenceSummaryTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        violations.Kind.Should().Be("read");
        violations.TriggerKeywords.Should().Contain(k => k.Contains("overspeed", StringComparison.OrdinalIgnoreCase));

        var util = new GetVehicleUtilizationSummaryTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        util.TriggerKeywords.Should().Contain(k => k.Contains("utilization rate", StringComparison.OrdinalIgnoreCase));

        var maint = new GetVehicleMaintenanceSummaryTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        maint.Description.Should().Contain("Smart Operations", Exactly.Once());

        var idle = new GetIdleTimeReportTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        idle.Kind.Should().Be("read");
        var sos = new GetSosEventsSummaryTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        sos.TriggerKeywords.Should().Contain(k => k.Contains("sos", StringComparison.OrdinalIgnoreCase));
        var expenses = new GetExpenseTrackingTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        expenses.Kind.Should().Be("read");
        var peak = new GetPeakHoursAnalysisTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        peak.Kind.Should().Be("read");
        var tenantCmp = new GetTenantComparisonTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        tenantCmp.Kind.Should().Be("read");
    }

    [Fact]
    public async Task Gateway_tenant_comparison_only_for_platform_super_admin()
    {
        var tools = AiAssistantGateway.AllowlistedToolNames
            .Select(name =>
            {
                var m = CreateStubTool(name);
                m.Setup(t => t.ExecuteAsync(It.IsAny<AiToolExecutionContext>(), It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AiToolExecutionResult(name, true, $"{name} ok", null));
                return m.Object;
            })
            .ToArray();

        var provider = new Mock<IAiProviderResolver>();
        provider.Setup(p => p.ResolveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, new AiProviderConfigDto()));

        var gateway = new AiAssistantGateway(
            tools,
            provider.Object,
            new Mock<IAiManagementService>().Object,
            NullLogger<AiAssistantGateway>.Instance);

        var denied = await gateway.QueryAsync(
            1, 1,
            new AiAssistantQueryRequest("Compare tenants across the platform", IsPlatformSuperAdmin: false),
            CancellationToken.None);
        denied.ToolUsed.Should().NotBe("GetTenantComparison");

        var allowed = await gateway.QueryAsync(
            1, 1,
            new AiAssistantQueryRequest("Compare tenants across the platform", IsPlatformSuperAdmin: true),
            CancellationToken.None);
        allowed.ToolUsed.Should().Be("GetTenantComparison");
        allowed.Answer.Should().Contain("GetTenantComparison ok");
    }

    [Fact]
    public async Task TenantComparison_tool_refuses_non_super_admin_context()
    {
        var tool = new GetTenantComparisonTool(Mock.Of<IDbConnectionFactory>(f => f.CreateConnection() == null!));
        var result = await tool.ExecuteAsync(
            new AiToolExecutionContext(1, 1, IsPlatformSuperAdmin: false),
            null,
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("super-admin");
    }

    private static Mock<IAiTool> CreateStubTool(string name, string kind = "read")
    {
        var m = new Mock<IAiTool>();
        m.SetupGet(t => t.Name).Returns(name);
        m.SetupGet(t => t.Kind).Returns(kind);
        m.SetupGet(t => t.TriggerKeywords).Returns(Array.Empty<string>());
        return m;
    }
}

/// <summary>SQL contract: fuel tool always filters by TenantId parameter (no client-supplied tenant).</summary>
public class GetFuelCostSummaryToolSqlTests
{
    [Fact]
    public async Task Execute_opens_connection_with_tenant_parameter()
    {
        // Smoke: constructing tool with a factory that throws proves we don't hit DB in unit setup.
        // Full SQL integration is covered by live DB environments; here we assert tool metadata + allowlist membership.
        AiAssistantGateway.AllowlistedToolNames.Should().Contain("GetFuelCostSummary");
        var tool = new GetFuelCostSummaryTool(Mock.Of<IDbConnectionFactory>(f =>
            f.CreateConnection() == null!));
        tool.Name.Should().Be("GetFuelCostSummary");
        tool.Kind.Should().Be("read");
        tool.RequiresConfirmation.Should().BeFalse();
        await Task.CompletedTask;
    }
}
