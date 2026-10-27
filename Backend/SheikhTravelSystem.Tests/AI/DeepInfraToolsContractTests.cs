using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Ai.Providers;

namespace SheikhTravelSystem.Tests.AI;

/// <summary>
/// Contract test: DeepInfra OpenAI-compatible tools payload shape + tool_calls parse (mocked HTTP).
/// Does not call live DeepInfra; verifies request/response wiring for MCP tool-calling.
/// </summary>
public class DeepInfraToolsContractTests
{
    [Fact]
    public async Task ChatAsync_sends_openai_tools_and_parses_tool_calls()
    {
        string? capturedBody = null;
        var handler = new RecordingHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var responseJson = """
                {
                  "id": "chatcmpl-test",
                  "model": "Qwen/Qwen3-32B",
                  "choices": [{
                    "index": 0,
                    "message": {
                      "role": "assistant",
                      "content": null,
                      "tool_calls": [{
                        "id": "call_1",
                        "type": "function",
                        "function": {
                          "name": "list_vehicles",
                          "arguments": "{\"page\":1}"
                        }
                      }]
                    },
                    "finish_reason": "tool_calls"
                  }],
                  "usage": { "prompt_tokens": 10, "completion_tokens": 5 }
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DeepInfra:ApiKey"] = "test-key-not-a-secret"
            })
            .Build();

        var factory = new NamedHttpClientFactory(new Dictionary<string, HttpMessageHandler>
        {
            [DeepInfraAiProvider.HttpClientName] = handler
        });

        var provider = new DeepInfraAiProvider(
            factory,
            config,
            NullLogger<DeepInfraAiProvider>.Instance);

        var tools = new List<AiLlmToolDefinition>
        {
            new(
                "list_vehicles",
                "List vehicles for the current tenant.",
                new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["page"] = new { type = "integer" }
                    }
                })
        };

        var result = await provider.ChatAsync(
            new AiProviderChatRequest(
                DeepInfraAiProvider.DefaultModel,
                [new AiChatMessageDto("user", "List vehicles")],
                DeepInfraAiProvider.DefaultBaseUrl,
                Temperature: 0.2,
                MaxTokens: 256,
                Tools: tools,
                ToolChoice: "auto"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ToolCalls.Should().NotBeNull().And.ContainSingle();
        var call = result.ToolCalls![0];
        call.Name.Should().Be("list_vehicles");
        call.ArgumentsJson.Should().Contain("page");

        capturedBody.Should().NotBeNullOrWhiteSpace();
        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        root.GetProperty("model").GetString().Should().Be(DeepInfraAiProvider.DefaultModel);
        root.GetProperty("tool_choice").GetString().Should().Be("auto");
        var toolsEl = root.GetProperty("tools");
        toolsEl.GetArrayLength().Should().Be(1);
        var fn = toolsEl[0].GetProperty("function");
        fn.GetProperty("name").GetString().Should().Be("list_vehicles");
        toolsEl[0].GetProperty("type").GetString().Should().Be("function");
        root.GetProperty("chat_template_kwargs").GetProperty("enable_thinking").GetBoolean()
            .Should().BeFalse();
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class NamedHttpClientFactory(Dictionary<string, HttpMessageHandler> handlers)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            if (!handlers.TryGetValue(name, out var handler))
                handler = handlers.Values.First();
            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.deepinfra.com/")
            };
        }
    }
}
