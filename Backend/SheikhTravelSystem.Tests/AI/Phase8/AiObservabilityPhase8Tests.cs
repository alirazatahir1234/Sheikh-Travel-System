using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Ai;
using SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

namespace SheikhTravelSystem.Tests.AI.Phase8;

/// <summary>Phase 8 observability — turn correlation shared across model → tools → MCP.</summary>
public class AiObservabilityPhase8Tests
{
    [Fact]
    public async Task Turn_correlation_id_is_stable_across_loop_and_not_regenerated_per_tool()
    {
        var ambient = new McpExecutionAmbient();
        var turnId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        ambient.CorrelationId = turnId;

        var bridge = new Mock<ISheikhGoMcpBridge>();
        bridge.SetupGet(b => b.IsEnabled).Returns(true);
        bridge.Setup(b => b.GetAllowlistedLlmToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AiLlmToolDefinition("list_vehicles", "v", new { type = "object", properties = new { } }),
                new AiLlmToolDefinition("list_drivers", "d", new { type = "object", properties = new { } })
            });

        // Simulate real bridge behavior: reuse ambient correlation.
        bridge.Setup(b => b.CallAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>(), It.IsAny<CancellationToken>()))
            .Returns<int, string, IReadOnlyDictionary<string, object?>?, CancellationToken>((_, name, _, _) =>
            {
                ambient.EnsureCorrelationId().Should().Be(turnId);
                ambient.ToolCallIndex++;
                ambient.McpLatencyMs += 5;
                return Task.FromResult(new McpToolCallResult(true, name, "{}"));
            });

        var provider = new Mock<IAiProvider>();
        provider.SetupGet(p => p.ProviderName).Returns("DeepInfra");
        provider.SetupSequence(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiProviderChatResult(true, string.Empty, "DeepInfra", "m", PromptTokens: 1, CompletionTokens: 1,
                ToolCalls:
                [
                    new AiToolCallDto("1", "list_vehicles", "{}"),
                    new AiToolCallDto("2", "list_drivers", "{}")
                ]))
            .ReturnsAsync(new AiProviderChatResult(true, "ok", "DeepInfra", "m", PromptTokens: 1, CompletionTokens: 1));

        var loop = new AiMcpToolCallingLoop(
            bridge.Object,
            Options.Create(new AiMcpBridgeOptions { Enabled = true, ProviderMaxRetries = 0, MaxToolRounds = 3 }),
            ambient,
            NullLogger<AiMcpToolCallingLoop>.Instance);

        var result = await loop.RunAsync(
            provider.Object, 1, "m", "https://x",
            [new AiChatMessageDto("user", "list")], new List<string>(),
            chatSessionId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        result!.Mode.Should().Be("mcp+llm");
        ambient.CorrelationId.Should().Be(turnId);
        ambient.ModelLatencyMs.Should().BeGreaterThanOrEqualTo(0);
        // Session cleared after loop; correlation retained for HTTP header.
        ambient.ChatSessionId.Should().BeNull();
    }

    [Fact]
    public void Bridge_reuses_ambient_correlation_instead_of_minting_new_turn_id()
    {
        var ambient = new McpExecutionAmbient { CorrelationId = "cccccccccccccccccccccccccccccccc" };
        // Direct unit of EnsureCorrelationId contract used by SheikhGoMcpBridge.
        ambient.EnsureCorrelationId().Should().Be("cccccccccccccccccccccccccccccccc");
        ambient.EnsureCorrelationId().Should().Be("cccccccccccccccccccccccccccccccc");
    }

    [Fact]
    public void Sanitizer_does_not_log_raw_secrets_in_preview()
    {
        var preview = McpToolExecutionLogger.SanitizeArgsPreview(new Dictionary<string, object?>
        {
            ["sql"] = "SELECT 1",
            ["password"] = "hunter2",
            ["authorization"] = "Bearer abc.def.ghi"
        });

        preview.Should().NotBeNull();
        preview!.Should().NotContain("hunter2");
        preview.Should().NotContain("Bearer abc");
    }
}
