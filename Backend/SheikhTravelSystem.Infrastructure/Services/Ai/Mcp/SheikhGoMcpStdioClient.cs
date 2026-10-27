using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

/// <summary>
/// Minimal MCP JSON-RPC client over stdio (newline-delimited). Spawns SheikhGo MCP once and reuses it.
/// Never enables <c>SHEIKHGO_ALLOW_WRITES</c>.
/// </summary>
public sealed class SheikhGoMcpStdioClient : IAsyncDisposable
{
    private const string ProtocolVersion = "2024-11-05";

    private readonly AiMcpBridgeOptions _options;
    private readonly ILogger<SheikhGoMcpStdioClient> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();

    private Process? _process;
    private StreamWriter? _stdin;
    private CancellationTokenSource? _readCts;
    private Task? _readLoop;
    private TaskCompletionSource<bool>? _stdioReady;
    private long _nextId;
    private bool _initialized;
    private string? _lastError;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SheikhGoMcpStdioClient(
        IOptions<AiMcpBridgeOptions> options,
        ILogger<SheikhGoMcpStdioClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string? LastError => _lastError;

    public bool IsConfigured
    {
        get
        {
            var (cmd, args) = ResolveCommand();
            return !string.IsNullOrWhiteSpace(cmd) && args.Length > 0;
        }
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureProcessLockedAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<JsonElement> CallAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureProcessLockedAsync(cancellationToken);
            return await SendRequestLockedAsync(method, parameters, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task NotifyAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureProcessLockedAsync(cancellationToken);
            await WriteMessageLockedAsync(new
            {
                jsonrpc = "2.0",
                method,
                @params = parameters
            }, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureProcessLockedAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false } && _initialized)
            return;

        await TearDownLockedAsync();

        var (command, args) = ResolveCommand();
        if (string.IsNullOrWhiteSpace(command) || args.Length == 0)
        {
            _lastError = "MCP bridge command not configured. Set Ai:McpBridge:Command/Args or SHEIKHGO_MCP_COMMAND.";
            throw new InvalidOperationException(_lastError);
        }

        // Encoding.UTF8 emits a BOM; MCP JSON-RPC parse then silently drops the first message.
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var psi = new ProcessStartInfo
        {
            FileName = command,
            WorkingDirectory = ResolveWorkingDirectory(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = utf8NoBom,
            StandardErrorEncoding = utf8NoBom,
            StandardInputEncoding = utf8NoBom,
        };

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        ApplyProcessEnvironment(psi);

        _logger.LogInformation(
            "Starting SheikhGo MCP: {Command} {Args} cwd={Cwd}",
            command, string.Join(' ', args), psi.WorkingDirectory);

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!_process.Start())
            throw new InvalidOperationException("Failed to start SheikhGo MCP process.");

        _stdin = new StreamWriter(_process.StandardInput.BaseStream, utf8NoBom)
        {
            AutoFlush = true,
            NewLine = "\n"
        };

        _stdioReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readLoop = Task.Run(() => ReadLoopAsync(_process.StandardOutput, _readCts.Token), CancellationToken.None);

        _ = Task.Run(() => DrainStderrAsync(_process.StandardError, _readCts.Token), CancellationToken.None);

        using var startupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startupCts.CancelAfter(Math.Max(5_000, _options.StartupTimeoutMs));

        try
        {
            // Optional: wait for MCP stderr banner so stdin listeners are attached.
            await WaitForStdioReadyAsync(startupCts.Token);

            var initResult = await SendRequestLockedAsync(
                "initialize",
                new
                {
                    protocolVersion = ProtocolVersion,
                    capabilities = new { },
                    clientInfo = new { name = "sheikhgo-aas-mcp-bridge", version = "1.0.0" }
                },
                startupCts.Token);

            if (initResult.ValueKind == JsonValueKind.Undefined)
                throw new InvalidOperationException("MCP initialize returned empty result.");

            await WriteMessageLockedAsync(new
            {
                jsonrpc = "2.0",
                method = "notifications/initialized"
            }, startupCts.Token);

            _initialized = true;
            _lastError = null;
            _logger.LogInformation("SheikhGo MCP initialize OK (protocol {Version})", ProtocolVersion);
        }
        catch
        {
            await TearDownLockedAsync();
            throw;
        }
    }

    private async Task WaitForStdioReadyAsync(CancellationToken cancellationToken)
    {
        var ready = _stdioReady;
        if (ready is null)
            return;

        var delayFallback = Task.Delay(250, cancellationToken);
        var completed = await Task.WhenAny(ready.Task, delayFallback);
        if (completed == ready.Task)
        {
            await ready.Task;
            await Task.Delay(30, cancellationToken);
            return;
        }

        _logger.LogDebug("MCP stderr ready banner not seen; proceeding after short delay");
    }

    private async Task<JsonElement> SendRequestLockedAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        if (_stdin is null || _process is null || _process.HasExited)
            throw new InvalidOperationException("MCP process is not running.");

        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, tcs))
            throw new InvalidOperationException("Failed to register MCP request id.");

        try
        {
            object payload = parameters is null
                ? new { jsonrpc = "2.0", id, method }
                : new { jsonrpc = "2.0", id, method, @params = parameters };

            await WriteMessageLockedAsync(payload, cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Math.Max(5_000, _options.CallTimeoutMs));

            await using var reg = timeout.Token.Register(
                () => tcs.TrySetException(new TimeoutException($"MCP call timed out: {method}")));

            return await tcs.Task;
        }
        catch
        {
            _pending.TryRemove(id, out _);
            throw;
        }
    }

    private async Task WriteMessageLockedAsync(object message, CancellationToken cancellationToken)
    {
        if (_stdin is null || _process is null)
            throw new InvalidOperationException("MCP stdin unavailable.");

        // Raw UTF-8 without BOM — Encoding.UTF8 can prepend EF BB BF; MCP then silently
        // drops the first JSON-RPC line (ReadBuffer continues on SyntaxError).
        var json = JsonSerializer.Serialize(message, JsonOpts) + "\n";
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
        cancellationToken.ThrowIfCancellationRequested();
        await _process.StandardInput.BaseStream.WriteAsync(bytes.AsMemory(), cancellationToken);
        await _process.StandardInput.BaseStream.FlushAsync(cancellationToken);
    }

    private async Task ReadLoopAsync(StreamReader stdout, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await stdout.ReadLineAsync(cancellationToken);
                if (line is null)
                    break;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(line);
                }
                catch (JsonException ex)
                {
                    _logger.LogDebug(ex, "Skipping non-JSON MCP stdout line");
                    continue;
                }

                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("id", out var idEl) &&
                        idEl.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                    {
                        long id = idEl.ValueKind == JsonValueKind.Number
                            ? idEl.GetInt64()
                            : long.TryParse(idEl.GetString(), out var parsed) ? parsed : -1;

                        if (id >= 0 && _pending.TryRemove(id, out var tcs))
                        {
                            if (root.TryGetProperty("error", out var err))
                            {
                                var msg = err.TryGetProperty("message", out var m)
                                    ? m.GetString() ?? "MCP error"
                                    : err.GetRawText();
                                tcs.TrySetException(new InvalidOperationException(msg));
                            }
                            else if (root.TryGetProperty("result", out var result))
                            {
                                tcs.TrySetResult(result.Clone());
                            }
                            else
                            {
                                tcs.TrySetResult(default);
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP stdout read loop ended");
            _lastError = ex.Message;
        }
        finally
        {
            foreach (var kv in _pending)
            {
                if (_pending.TryRemove(kv.Key, out var tcs))
                    tcs.TrySetException(new InvalidOperationException("MCP process closed."));
            }
            _initialized = false;
        }
    }

    private async Task DrainStderrAsync(StreamReader stderr, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await stderr.ReadLineAsync(cancellationToken);
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                _logger.LogDebug("MCP stderr: {Line}", line);

                // SheikhGo MCP logs this after serveStdio() starts listening on stdin.
                if (line.Contains("running on stdio", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("[sheikhgo-mcp]", StringComparison.OrdinalIgnoreCase))
                {
                    _stdioReady?.TrySetResult(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MCP stderr drain ended");
        }
    }

    private (string? Command, string[] Args) ResolveCommand()
    {
        var envCmd = Environment.GetEnvironmentVariable("SHEIKHGO_MCP_COMMAND");
        if (!string.IsNullOrWhiteSpace(envCmd))
        {
            var envArgs = Environment.GetEnvironmentVariable("SHEIKHGO_MCP_ARGS");
            return (envCmd.Trim(), ParseArgs(envArgs, _options.Args));
        }

        if (!string.IsNullOrWhiteSpace(_options.Command))
            return (_options.Command.Trim(), _options.Args ?? []);

        // Sensible local default: sibling SheikhGo-MCP dist entry
        var repo = ResolveRepositoryPath();
        if (!string.IsNullOrWhiteSpace(repo))
        {
            var sibling = Path.GetFullPath(Path.Combine(repo, "..", "SheikhGo-MCP", "dist", "index.js"));
            if (File.Exists(sibling))
                return ("node", [sibling]);
        }

        return (null, []);
    }

    private static string[] ParseArgs(string? envArgs, string[]? configured)
    {
        if (!string.IsNullOrWhiteSpace(envArgs))
        {
            var trimmed = envArgs.Trim();
            if (trimmed.StartsWith('['))
            {
                try
                {
                    var arr = JsonSerializer.Deserialize<string[]>(trimmed);
                    if (arr is { Length: > 0 })
                        return arr;
                }
                catch (JsonException)
                {
                    // fall through
                }
            }

            return [trimmed];
        }

        return configured ?? [];
    }

    private string ResolveWorkingDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
            return _options.WorkingDirectory;

        var repo = ResolveRepositoryPath();
        if (!string.IsNullOrWhiteSpace(repo))
        {
            var mcpRoot = Path.GetFullPath(Path.Combine(repo, "..", "SheikhGo-MCP"));
            if (Directory.Exists(mcpRoot))
                return mcpRoot;
        }

        return Directory.GetCurrentDirectory();
    }

    private string? ResolveRepositoryPath()
    {
        var env = Environment.GetEnvironmentVariable("SHEIKHGO_REPOSITORY");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();
        if (!string.IsNullOrWhiteSpace(_options.RepositoryPath))
            return _options.RepositoryPath.Trim();
        return null;
    }

    private void ApplyProcessEnvironment(ProcessStartInfo psi)
    {
        // Hard deny writes for tenant SheikhGo AI Assistant bridge
        psi.Environment["SHEIKHGO_ALLOW_WRITES"] = "false";

        var repo = ResolveRepositoryPath();
        if (!string.IsNullOrWhiteSpace(repo))
            psi.Environment["SHEIKHGO_REPOSITORY"] = repo;

        // Optional live DB — prefer dedicated read-only env; never invent credentials here
        var db = Environment.GetEnvironmentVariable("SHEIKHGO_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(db))
            db = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(db))
            psi.Environment["SHEIKHGO_DB_CONNECTION"] = db;

        // Optional Traccar (read-only HTTP) — pass through if already in host env
        foreach (var key in new[]
                 {
                     "SHEIKHGO_TRACCAR_ENABLED",
                     "SHEIKHGO_TRACCAR_BASE_URL",
                     "SHEIKHGO_TRACCAR_USERNAME",
                     "SHEIKHGO_TRACCAR_PASSWORD",
                     "SHEIKHGO_TRACCAR_TIMEOUT_MS",
                     "SHEIKHGO_DB_MAX_ROWS",
                     "SHEIKHGO_DB_QUERY_TIMEOUT_MS"
                 })
        {
            var val = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(val))
                psi.Environment[key] = val;
        }
    }

    private async Task TearDownLockedAsync()
    {
        _initialized = false;
        try { _readCts?.Cancel(); } catch { /* ignore */ }

        if (_readLoop is not null)
        {
            try { await Task.WhenAny(_readLoop, Task.Delay(1000)); } catch { /* ignore */ }
        }

        try { _stdin?.Dispose(); } catch { /* ignore */ }
        _stdin = null;
        _stdioReady?.TrySetCanceled();
        _stdioReady = null;

        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(2000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "MCP process kill");
            }

            try { _process.Dispose(); } catch { /* ignore */ }
            _process = null;
        }

        _readCts?.Dispose();
        _readCts = null;
        _readLoop = null;
        _pending.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await TearDownLockedAsync();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    /// <summary>Extract MCP tool call text content from tools/call result.</summary>
    public static string ExtractToolContent(JsonElement result)
    {
        if (result.ValueKind == JsonValueKind.Undefined || result.ValueKind == JsonValueKind.Null)
            return string.Empty;

        if (result.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var item in content.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase) &&
                    item.TryGetProperty("text", out var text))
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(text.GetString());
                }
            }

            if (sb.Length > 0)
                return sb.ToString();
        }

        if (result.TryGetProperty("isError", out var isErr) &&
            isErr.ValueKind == JsonValueKind.True)
        {
            return result.GetRawText();
        }

        return result.GetRawText();
    }

    public static IReadOnlyList<(string Name, string Description, JsonNode? InputSchema)> ParseToolsList(
        JsonElement result)
    {
        var list = new List<(string, string, JsonNode?)>();
        if (!result.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var tool in tools.EnumerateArray())
        {
            var name = tool.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var desc = tool.TryGetProperty("description", out var d) ? d.GetString() ?? name : name;
            JsonNode? schema = null;
            if (tool.TryGetProperty("inputSchema", out var schemaEl))
            {
                try { schema = JsonNode.Parse(schemaEl.GetRawText()); }
                catch { /* ignore */ }
            }

            list.Add((name, desc, schema));
        }

        return list;
    }
}
