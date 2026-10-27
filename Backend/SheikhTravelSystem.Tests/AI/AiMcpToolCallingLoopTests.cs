using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Ai;

namespace SheikhTravelSystem.Tests.AI;

/// <summary>
/// Phase 2 — robust DeepInfra ↔ MCP tool-calling orchestration loop.
/// </summary>
public class AiMcpToolCallingLoopTests
{
    private static readonly AiLlmToolDefinition ListVehiclesTool = new(
        "list_vehicles",
        "List vehicles",
        new { type = "object", properties = new { page = new { type = "integer" } } });

    private static readonly AiLlmToolDefinition ListDriversTool = new(
        "list_drivers",
        "List drivers",
        new { type = "object", properties = new { } });

    [Fact]
    public async Task Normal_text_no_tools_returns_answer()
    {
        var (loop, provider, bridge) = CreateLoop();
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });
        provider.Setup(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok("Fleet overview looks healthy."));

        var tools = new List<string>();
        var result = await loop.RunAsync(
            provider.Object, 1, "Qwen/Qwen3-32B", "https://api.deepinfra.com/v1/openai",
            [new AiChatMessageDto("user", "How is my fleet?")], tools);

        result.Should().NotBeNull();
        result!.Mode.Should().Be("mcp+llm");
        result.Answer.Should().Be("Fleet overview looks healthy.");
        result.SuccessfulToolCount.Should().Be(0);
        bridge.Verify(
            b => b.CallAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task One_tool_call_executes_and_returns_final_answer()
    {
        var (loop, provider, bridge) = CreateLoop();
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });
        bridge.Setup(b => b.CallAsync(1, "list_vehicles", It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolCallResult(true, "list_vehicles", """{"rows":[{"Reg":"ABC"}]}"""));

        provider.SetupSequence(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkTools(new AiToolCallDto("call_1", "list_vehicles", """{"page":1}""")))
            .ReturnsAsync(Ok("You have 1 vehicle (ABC)."));

        var tools = new List<string>();
        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "How many vehicles?")], tools);

        result!.Answer.Should().Contain("1 vehicle");
        result.Mode.Should().Be("mcp+llm");
        result.SuccessfulToolCount.Should().Be(1);
        tools.Should().Contain("mcp:list_vehicles");
        bridge.Verify(
            b => b.CallAsync(1, "list_vehicles", It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Multiple_sequential_tool_rounds_execute_in_order()
    {
        var (loop, provider, bridge) = CreateLoop(o => o.MaxToolRounds = 4);
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool, ListDriversTool });

        var callOrder = new List<string>();
        bridge.Setup(b => b.CallAsync(1, It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .Returns<int, string, IReadOnlyDictionary<string, object?>?, CancellationToken>((_, name, _, _) =>
            {
                callOrder.Add(name);
                return Task.FromResult(new McpToolCallResult(true, name, """{"rows":[]}"""));
            });

        provider.SetupSequence(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkTools(new AiToolCallDto("c1", "list_vehicles", "{}")))
            .ReturnsAsync(OkTools(new AiToolCallDto("c2", "list_drivers", "{}")))
            .ReturnsAsync(Ok("Vehicles and drivers listed."));

        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "List vehicles then drivers")], new List<string>());

        result!.Mode.Should().Be("mcp+llm");
        callOrder.Should().Equal("list_vehicles", "list_drivers");
        result.SuccessfulToolCount.Should().Be(2);
    }

    [Fact]
    public async Task Invalid_tool_name_does_not_call_mcp_and_returns_safe_ux()
    {
        var (loop, provider, bridge) = CreateLoop();
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });

        provider.SetupSequence(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkTools(new AiToolCallDto("call_x", "drop_database", "{}")))
            .ReturnsAsync(Ok("There are 99 secret vehicles.")); // must be ignored

        var tools = new List<string>();
        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "hack")], tools);

        result!.Mode.Should().Be("mcp+llm_error");
        result.Answer.Should().Be(AiMcpToolCallingLoop.SafeUserFailureMessage);
        result.HadToolFailures.Should().BeTrue();
        bridge.Verify(
            b => b.CallAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Invalid_args_rejected_without_mcp_call()
    {
        var (loop, provider, bridge) = CreateLoop();
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });

        provider.SetupSequence(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkTools(new AiToolCallDto("call_1", "list_vehicles", "{not-json")))
            .ReturnsAsync(Ok("Invented answer"));

        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "vehicles")], new List<string>());

        result!.Mode.Should().Be("mcp+llm_error");
        result.Answer.Should().Be(AiMcpToolCallingLoop.SafeUserFailureMessage);
        bridge.Verify(
            b => b.CallAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Tool_timeout_returns_safe_ux_not_exception_details()
    {
        var (loop, provider, bridge) = CreateLoop(o =>
        {
            o.CallTimeoutMs = 80;
            o.ProviderMaxRetries = 0;
        });
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });
        bridge.Setup(b => b.CallAsync(1, "list_vehicles", It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .Returns(async (int _, string _, IReadOnlyDictionary<string, object?>? __, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new McpToolCallResult(true, "list_vehicles", "{}");
            });

        provider.SetupSequence(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkTools(new AiToolCallDto("call_1", "list_vehicles", "{}")))
            .ReturnsAsync(Ok("Should not use this fabricated count: 42"));

        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "vehicles")], new List<string>());

        result!.Mode.Should().Be("mcp+llm_error");
        result.Answer.Should().Be(AiMcpToolCallingLoop.SafeUserFailureMessage);
        result.Answer.Should().NotContain("Exception");
        result.Answer.Should().NotContain("42");
    }

    [Fact]
    public async Task Provider_timeout_retries_then_safe_ux()
    {
        var (loop, provider, bridge) = CreateLoop(o =>
        {
            o.ProviderMaxRetries = 2;
            o.ProviderRetryBaseDelayMs = 1;
        });
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });

        provider.SetupSequence(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Fail("DeepInfra timed out. Retry or reduce max tokens."))
            .ReturnsAsync(Fail("DeepInfra timed out. Retry or reduce max tokens."))
            .ReturnsAsync(Fail("DeepInfra timed out. Retry or reduce max tokens."));

        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "hi")], new List<string>());

        result!.Mode.Should().Be("mcp+llm_error");
        result.Answer.Should().Be(AiMcpToolCallingLoop.SafeUserFailureMessage);
        provider.Verify(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Provider_rate_limit_retries_then_succeeds_without_duplicate_tools()
    {
        var (loop, provider, bridge) = CreateLoop(o =>
        {
            o.ProviderMaxRetries = 2;
            o.ProviderRetryBaseDelayMs = 1;
        });
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });
        bridge.Setup(b => b.CallAsync(1, "list_vehicles", It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolCallResult(true, "list_vehicles", """{"rows":[{"Id":1}]}"""));

        // Round 1: rate limit then tool_calls. Round 2: final text.
        provider.SetupSequence(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Fail("DeepInfra returned 429 rate limit. Retry shortly."))
            .ReturnsAsync(OkTools(new AiToolCallDto("call_1", "list_vehicles", "{}")))
            .ReturnsAsync(Ok("1 vehicle."));

        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "count")], new List<string>());

        result!.Answer.Should().Contain("1 vehicle");
        bridge.Verify(
            b => b.CallAsync(1, "list_vehicles", It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Empty_tool_result_distinguished_and_answered()
    {
        var (loop, provider, bridge) = CreateLoop();
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });
        bridge.Setup(b => b.CallAsync(1, "list_vehicles", It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolCallResult(true, "list_vehicles", "   "));

        AiProviderChatRequest? followUp = null;
        var queue = new Queue<AiProviderChatResult>();
        queue.Enqueue(OkTools(new AiToolCallDto("call_1", "list_vehicles", "{}")));
        queue.Enqueue(Ok("No vehicles found for your fleet."));
        provider.Setup(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Dequeue())
            .Callback<AiProviderChatRequest, CancellationToken>((req, _) =>
            {
                if (req.Messages.Any(m => m.Role == "tool"))
                    followUp = req;
            });

        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "vehicles")], new List<string>());

        result!.Mode.Should().Be("mcp+llm");
        result.SuccessfulToolCount.Should().Be(1);
        result.HadToolFailures.Should().BeFalse();
        followUp.Should().NotBeNull();
        followUp!.Messages.Should().Contain(m =>
            m.Role == "tool" && m.Content.StartsWith("EMPTY_RESULT", StringComparison.Ordinal));
        result.Answer.Should().Contain("No vehicles");
    }

    [Fact]
    public async Task Max_iterations_stops_and_does_not_fabricate_without_success()
    {
        var (loop, provider, bridge) = CreateLoop(o =>
        {
            o.MaxToolRounds = 2;
            o.ProviderMaxRetries = 0;
        });
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });
        bridge.Setup(b => b.CallAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolCallResult(false, "list_vehicles", string.Empty, "boom"));

        var toolRound = 0;
        provider.Setup(p => p.ChatAsync(
                It.Is<AiProviderChatRequest>(r => r.Tools != null && r.Tools.Count > 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                toolRound++;
                return OkTools(new AiToolCallDto($"call_{toolRound}", "list_vehicles", "{}"));
            });

        provider.Setup(p => p.ChatAsync(
                It.Is<AiProviderChatRequest>(r => r.Tools == null || r.Tools.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok("Definitely 77 vehicles exist."));

        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "count")], new List<string>());

        result!.Mode.Should().Be("mcp+llm_error");
        result.Answer.Should().Be(AiMcpToolCallingLoop.SafeUserFailureMessage);
        result.Answer.Should().NotContain("77");
        toolRound.Should().Be(2);
    }

    [Fact]
    public async Task Cancellation_propagates()
    {
        var (loop, provider, bridge) = CreateLoop();
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "hi")], new List<string>(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Tool_failure_safe_ux_no_fabricated_business_answer()
    {
        var (loop, provider, bridge) = CreateLoop();
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });
        bridge.Setup(b => b.CallAsync(1, "list_vehicles", It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolCallResult(false, "list_vehicles", string.Empty, "SQL connection string leaked!!!"));

        provider.SetupSequence(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkTools(new AiToolCallDto("call_1", "list_vehicles", "{}")))
            .ReturnsAsync(Ok("You have exactly 12 vehicles."));

        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "how many?")], new List<string>());

        result!.Mode.Should().Be("mcp+llm_error");
        result.Answer.Should().Be(AiMcpToolCallingLoop.SafeUserFailureMessage);
        result.Answer.Should().NotContain("12");
        result.Answer.Should().NotContain("SQL");
        result.Answer.Should().NotContain("connection");
    }

    [Fact]
    public async Task Tool_messages_use_correct_tool_call_id()
    {
        var (loop, provider, bridge) = CreateLoop();
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });
        bridge.Setup(b => b.CallAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolCallResult(true, "list_vehicles", """{"ok":true}"""));

        AiProviderChatRequest? followUp = null;
        var queue = new Queue<AiProviderChatResult>();
        queue.Enqueue(OkTools(new AiToolCallDto("call_abc123", "list_vehicles", "{}")));
        queue.Enqueue(Ok("done"));
        provider.Setup(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Dequeue())
            .Callback<AiProviderChatRequest, CancellationToken>((req, _) =>
            {
                if (req.Messages.Any(m => m.Role == "tool"))
                    followUp = req;
            });

        await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "v")], new List<string>());

        followUp.Should().NotBeNull();
        var toolMsg = followUp!.Messages.First(m => m.Role == "tool");
        toolMsg.ToolCallId.Should().Be("call_abc123");
        var assistant = followUp.Messages.Last(m => m.Role == "assistant" && m.ToolCalls is { Count: > 0 });
        assistant.ToolCalls![0].Id.Should().Be("call_abc123");
    }

    [Fact]
    public async Task Strips_tenantId_from_model_args_before_bridge()
    {
        var (loop, provider, bridge) = CreateLoop();
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });

        IReadOnlyDictionary<string, object?>? captured = null;
        bridge.Setup(b => b.CallAsync(9, "list_vehicles", It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, IReadOnlyDictionary<string, object?>?, CancellationToken>((_, _, args, _) => captured = args)
            .ReturnsAsync(new McpToolCallResult(true, "list_vehicles", "{}"));

        provider.SetupSequence(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkTools(new AiToolCallDto("c1", "list_vehicles", """{"tenantId":999,"page":1}""")))
            .ReturnsAsync(Ok("ok"));

        await loop.RunAsync(
            provider.Object, 9, "m", "https://x",
            [new AiChatMessageDto("user", "v")], new List<string>());

        captured.Should().NotBeNull();
        captured!.Keys.Should().NotContain(k => k.Equals("tenantId", StringComparison.OrdinalIgnoreCase));
        captured.Should().ContainKey("page");
    }

    [Fact]
    public async Task Duplicate_tool_call_id_not_executed_twice()
    {
        var (loop, provider, bridge) = CreateLoop();
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ListVehiclesTool });
        bridge.Setup(b => b.CallAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpToolCallResult(true, "list_vehicles", "{}"));

        provider.SetupSequence(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkTools(
                new AiToolCallDto("same", "list_vehicles", "{}"),
                new AiToolCallDto("same", "list_vehicles", "{}")))
            .ReturnsAsync(Ok("1 vehicle"));

        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "v")], new List<string>());

        result!.SuccessfulToolCount.Should().Be(1);
        bridge.Verify(
            b => b.CallAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void TryParse_and_transient_helpers()
    {
        AiMcpToolCallingLoop.TryParseToolArguments("""{"a":1}""", out var args, out _)
            .Should().BeTrue();
        args.Should().ContainKey("a");

        AiMcpToolCallingLoop.TryParseToolArguments("{bad", out _, out var err)
            .Should().BeFalse();
        err.Should().NotBeNullOrWhiteSpace();

        AiMcpToolCallingLoop.IsTransientProviderError("DeepInfra returned 429 rate limit.")
            .Should().BeTrue();
        AiMcpToolCallingLoop.IsTransientProviderError("DeepInfra returned 401.")
            .Should().BeFalse();
    }

    [Fact]
    public async Task Returns_null_when_bridge_unavailable()
    {
        var bridge = new Mock<ISheikhGoMcpBridge>();
        bridge.SetupGet(b => b.IsEnabled).Returns(false);
        var ambient = CreateAmbientMock();
        var loop = new AiMcpToolCallingLoop(
            bridge.Object,
            Options.Create(new AiMcpBridgeOptions()),
            ambient.Object,
            NullLogger<AiMcpToolCallingLoop>.Instance);

        var result = await loop.RunAsync(
            Mock.Of<IAiProvider>(), 1, "m", "https://x",
            [new AiChatMessageDto("user", "hi")], new List<string>());

        result.Should().BeNull();
    }

    private static (AiMcpToolCallingLoop Loop, Mock<IAiProvider> Provider, Mock<ISheikhGoMcpBridge> Bridge)
        CreateLoop(Action<AiMcpBridgeOptions>? configure = null)
    {
        var opts = new AiMcpBridgeOptions
        {
            Enabled = true,
            MaxToolRounds = 3,
            CallTimeoutMs = 5_000,
            ProviderMaxRetries = 0,
            ProviderRetryBaseDelayMs = 1,
            MaxHistoryMessages = 20
        };
        configure?.Invoke(opts);

        var bridge = new Mock<ISheikhGoMcpBridge>();
        bridge.SetupGet(b => b.IsEnabled).Returns(true);

        var provider = new Mock<IAiProvider>();
        provider.SetupGet(p => p.ProviderName).Returns("DeepInfra");

        var ambient = CreateAmbientMock();

        var loop = new AiMcpToolCallingLoop(
            bridge.Object,
            Options.Create(opts),
            ambient.Object,
            NullLogger<AiMcpToolCallingLoop>.Instance);

        return (loop, provider, bridge);
    }

    private static Mock<IMcpExecutionAmbient> CreateAmbientMock()
    {
        var ambient = new Mock<IMcpExecutionAmbient>();
        ambient.SetupProperty(a => a.ChatSessionId);
        ambient.SetupProperty(a => a.CorrelationId);
        ambient.SetupProperty(a => a.ToolCallIndex);
        ambient.SetupProperty(a => a.ModelLatencyMs);
        ambient.SetupProperty(a => a.McpLatencyMs);
        ambient.Setup(a => a.EnsureCorrelationId())
            .Returns(() =>
            {
                if (string.IsNullOrWhiteSpace(ambient.Object.CorrelationId))
                    ambient.Object.CorrelationId = Guid.NewGuid().ToString("N");
                return ambient.Object.CorrelationId!;
            });
        return ambient;
    }

    private static AiProviderChatResult Ok(string content) =>
        new(true, content, "DeepInfra", "Qwen/Qwen3-32B", PromptTokens: 10, CompletionTokens: 5);

    private static AiProviderChatResult OkTools(params AiToolCallDto[] calls) =>
        new(true, string.Empty, "DeepInfra", "Qwen/Qwen3-32B", PromptTokens: 10, CompletionTokens: 5,
            ToolCalls: calls);

    private static AiProviderChatResult Fail(string error) =>
        new(false, string.Empty, "DeepInfra", "Qwen/Qwen3-32B", Error: error);
}
