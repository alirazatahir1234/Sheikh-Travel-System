using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

public sealed class McpExecutionAmbient : IMcpExecutionAmbient
{
    public Guid? ChatSessionId { get; set; }
    public string? CorrelationId { get; set; }
    public int ToolCallIndex { get; set; }
    public long ModelLatencyMs { get; set; }
    public long McpLatencyMs { get; set; }

    public string EnsureCorrelationId()
    {
        if (string.IsNullOrWhiteSpace(CorrelationId))
            CorrelationId = Guid.NewGuid().ToString("N");
        return CorrelationId!;
    }

    public void Clear()
    {
        ChatSessionId = null;
        CorrelationId = null;
        ToolCallIndex = 0;
        ModelLatencyMs = 0;
        McpLatencyMs = 0;
    }
}
