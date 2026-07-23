using System.Text.Json;

namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>Secure tool surface for AI — never lets the LLM touch SQL directly.</summary>
public interface IAiToolEngine
{
    IReadOnlyList<AiToolDescriptor> ListTools(bool includeWriteTools = true);

    Task<IReadOnlyList<AiToolExecutionResult>> SelectAndExecuteAsync(
        AiToolExecutionContext context,
        string userMessage,
        CancellationToken cancellationToken = default);

    Task<AiToolExecutionResult> ExecuteAsync(
        string toolName,
        AiToolExecutionContext context,
        JsonElement? args = null,
        CancellationToken cancellationToken = default);
}

public interface IAiTool
{
    string Name { get; }
    string Description { get; }
    /// <summary>read | write</summary>
    string Kind { get; }
    bool RequiresConfirmation { get; }
    IReadOnlyList<string> TriggerKeywords { get; }

    Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context,
        JsonElement? args,
        CancellationToken cancellationToken = default);
}

public record AiToolDescriptor(
    string Name,
    string Description,
    string Kind,
    bool RequiresConfirmation);

public record AiToolExecutionContext(
    int TenantId,
    int UserId,
    bool AllowWriteTools = false,
    bool ConfirmWrite = false,
    bool CanExecuteWrite = false);

public record AiToolExecutionResult(
    string ToolName,
    bool Success,
    string Summary,
    object? Data = null,
    bool PendingConfirmation = false,
    string? Error = null);
