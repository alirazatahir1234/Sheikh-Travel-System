using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

/// <summary>
/// Best-effort insert into McpToolExecutions. Failures are logged and swallowed
/// so diagnostics never break the assistant tool path.
/// </summary>
public sealed class McpToolExecutionLogger(
    IDbConnectionFactory dbFactory,
    ILogger<McpToolExecutionLogger> logger) : IMcpToolExecutionLogger
{
    private static readonly Regex SecretPattern = new(
        @"(Password\s*=\s*)[^\s;""']+|(Pwd\s*=\s*)[^\s;""']+|(Api[_-]?Key\s*[=:]\s*)[^\s;,""']+|(Secret\s*[=:]\s*)[^\s;,""']+|(Bearer\s+)[A-Za-z0-9\-._~+/]+=*|(eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,})|(access[_-]?token|refresh[_-]?token)\s*[=:]\s*[^\s;,""']+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmailPattern = new(
        @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task LogAsync(McpToolExecutionLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.TenantId <= 0 || string.IsNullOrWhiteSpace(entry.ToolName))
            return;

        try
        {
            using var connection = dbFactory.CreateConnection();
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO dbo.McpToolExecutions
                    (TenantId, ToolName, Success, DurationMs, CorrelationId, SessionId, UserId, ArgsSanitized, ErrorSanitized, CreatedAt)
                VALUES
                    (@TenantId, @ToolName, @Success, @DurationMs, @CorrelationId, @SessionId, @UserId, @ArgsSanitized, @ErrorSanitized, GETUTCDATE())
                """,
                new
                {
                    entry.TenantId,
                    ToolName = Truncate(entry.ToolName.Trim(), 128),
                    entry.Success,
                    DurationMs = Math.Clamp(entry.DurationMs, 0, int.MaxValue),
                    CorrelationId = Truncate(entry.CorrelationId, 64),
                    entry.SessionId,
                    entry.UserId,
                    ArgsSanitized = Truncate(Sanitize(entry.ArgsSanitized), 1000),
                    ErrorSanitized = Truncate(Sanitize(entry.ErrorSanitized), 500),
                },
                cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            // Table may not exist yet on first boot before migration — never break MCP.
            logger.LogDebug(ex, "McpToolExecutions insert skipped for tool={Tool}", entry.ToolName);
        }
    }

    /// <summary>Build a compact sanitized args preview from a dictionary.</summary>
    public static string SanitizeArgsPreview(IReadOnlyDictionary<string, object?>? args)
    {
        if (args is null || args.Count == 0)
            return "{}";

        try
        {
            var safe = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in args)
            {
                if (IsSecretKey(kv.Key))
                {
                    safe[kv.Key] = "[REDACTED]";
                    continue;
                }

                safe[kv.Key] = kv.Value switch
                {
                    null => null,
                    string s => Truncate(Sanitize(s), 120),
                    bool or int or long or double or decimal or float => kv.Value,
                    _ => Truncate(Sanitize(kv.Value.ToString()), 80)
                };
            }

            return Truncate(Sanitize(JsonSerializer.Serialize(safe)), 1000) ?? "{}";
        }
        catch
        {
            return "{…}";
        }
    }

    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var v = SecretPattern.Replace(value, m =>
        {
            if (m.Groups[1].Success) return m.Groups[1].Value + "[REDACTED]";
            if (m.Groups[2].Success) return m.Groups[2].Value + "[REDACTED]";
            if (m.Groups[3].Success) return m.Groups[3].Value + "[REDACTED]";
            if (m.Groups[4].Success) return m.Groups[4].Value + "[REDACTED]";
            if (m.Groups[5].Success) return m.Groups[5].Value + "[REDACTED]";
            if (m.Groups[6].Success) return "[REDACTED]";
            if (m.Groups[7].Success) return m.Groups[7].Value + "=[REDACTED]";
            return "[REDACTED]";
        });
        v = EmailPattern.Replace(v, "[EMAIL]");
        return v;
    }

    private static bool IsSecretKey(string key)
    {
        var k = key.ToLowerInvariant();
        return k.Contains("password", StringComparison.Ordinal)
               || k.Contains("secret", StringComparison.Ordinal)
               || k.Contains("token", StringComparison.Ordinal)
               || k.Contains("apikey", StringComparison.Ordinal)
               || k.Contains("api_key", StringComparison.Ordinal)
               || k.Contains("connectionstring", StringComparison.Ordinal)
               || k.Contains("authorization", StringComparison.Ordinal);
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        var t = value.Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }
}
