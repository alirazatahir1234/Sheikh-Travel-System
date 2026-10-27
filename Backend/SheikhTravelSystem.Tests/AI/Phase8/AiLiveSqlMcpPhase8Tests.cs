using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

namespace SheikhTravelSystem.Tests.AI.Phase8;

/// <summary>
/// Phase 8 live SQL + MCP stdio E2E. Seeds fixture tenants 9001/9002, calls real SheikhGo-MCP
/// via <see cref="SheikhGoMcpBridge"/>, asserts counts and cross-tenant isolation.
/// Skips when SQL/MCP unavailable; fails when <c>SHEIKHGO_PHASE8_LIVE_SQL=require</c>.
/// </summary>
public class AiLiveSqlMcpPhase8Tests : IAsyncLifetime
{
    private SheikhGoMcpStdioClient? _client;
    private Phase8LiveSqlFixture.Availability _probe = Phase8LiveSqlFixture.Probe();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();
    }

    [Fact]
    public async Task Live_get_vehicle_count_isolates_tenants_9001_and_9002()
    {
        if (!_probe.Available)
        {
            if (_probe.RequireLive)
                Assert.Fail(_probe.SkipReason ?? "Live SQL required but unavailable");
            return; // soft skip — xUnit has no Skip.If without Xunit.SkippableFact; treat as pass-with-note
        }

        var bridge = await CreateBridgeAsync();
        var a = await bridge.CallAsync(
            Phase8LiveSqlFixture.TenantA,
            "get_vehicle_count",
            new Dictionary<string, object?>(),
            CancellationToken.None);
        var b = await bridge.CallAsync(
            Phase8LiveSqlFixture.TenantB,
            "get_vehicle_count",
            new Dictionary<string, object?>(),
            CancellationToken.None);

        a.Success.Should().BeTrue(because: a.Error ?? a.Content);
        b.Success.Should().BeTrue(because: b.Error ?? b.Content);

        ReadCount(a.Content).Should().Be(Phase8LiveSqlFixture.ExpectedCountA);
        ReadCount(b.Content).Should().Be(Phase8LiveSqlFixture.ExpectedCountB);
        ReadCount(a.Content).Should().NotBe(ReadCount(b.Content));
    }

    [Fact]
    public async Task Live_forged_tenantId_in_args_cannot_cross_tenant()
    {
        if (!_probe.Available)
        {
            if (_probe.RequireLive)
                Assert.Fail(_probe.SkipReason ?? "Live SQL required but unavailable");
            return;
        }

        var bridge = await CreateBridgeAsync();

        // Model forges tenantId=9002 while server auth tenant is 9001.
        var forged = await bridge.CallAsync(
            Phase8LiveSqlFixture.TenantA,
            "get_vehicle_count",
            new Dictionary<string, object?>
            {
                ["tenantId"] = Phase8LiveSqlFixture.TenantB,
                ["companyId"] = 9999,
                ["targetTenantId"] = Phase8LiveSqlFixture.TenantB,
                ["limit"] = 50
            },
            CancellationToken.None);

        forged.Success.Should().BeTrue(because: forged.Error ?? forged.Content);
        ReadCount(forged.Content).Should().Be(
            Phase8LiveSqlFixture.ExpectedCountA,
            "forged body/args must not leak tenant 9002 counts into tenant 9001 call");
        ReadTenantId(forged.Content).Should().Be(Phase8LiveSqlFixture.TenantA);
    }

    [Fact]
    public async Task Live_SuperAdmin_company_scope_uses_resolved_tenant_only()
    {
        if (!_probe.Available)
        {
            if (_probe.RequireLive)
                Assert.Fail(_probe.SkipReason ?? "Live SQL required but unavailable");
            return;
        }

        // HTTP layer resolves SuperAdmin targetTenantId → 9002 before bridge.
        // Bridge must then force that tenant regardless of forged args pointing at 9001.
        var bridge = await CreateBridgeAsync(isSuperAdmin: true);
        var scoped = AiEffectiveTenantResolver.Resolve(
            contextTenantId: Phase8LiveSqlFixture.TenantA,
            isPlatformSuperAdmin: true,
            requestedTargetTenantId: Phase8LiveSqlFixture.TenantB);
        scoped.Status.Should().Be(AiEffectiveTenantResolver.ResolveStatus.Ok);

        var result = await bridge.CallAsync(
            scoped.TenantId,
            "get_vehicle_count",
            new Dictionary<string, object?>
            {
                ["tenantId"] = Phase8LiveSqlFixture.TenantA,
                ["companyId"] = Phase8LiveSqlFixture.TenantA
            },
            CancellationToken.None);

        result.Success.Should().BeTrue(because: result.Error ?? result.Content);
        ReadCount(result.Content).Should().Be(Phase8LiveSqlFixture.ExpectedCountB);
        ReadTenantId(result.Content).Should().Be(Phase8LiveSqlFixture.TenantB);
    }

    private async Task<SheikhGoMcpBridge> CreateBridgeAsync(bool isSuperAdmin = false)
    {
        _probe = Phase8LiveSqlFixture.Probe();
        if (!_probe.Available || _probe.ConnectionString is null || _probe.McpEntryPath is null)
            throw new InvalidOperationException(_probe.SkipReason ?? "Live probe failed");

        Phase8LiveSqlFixture.EnsureFixtures(_probe.ConnectionString);
        var phase8Cs = Phase8LiveSqlFixture.Phase8DbConnection(_probe.ConnectionString);

        // Pass DB to MCP child without printing secrets.
        Environment.SetEnvironmentVariable("SHEIKHGO_DB_CONNECTION", phase8Cs);

        var opts = Options.Create(new AiMcpBridgeOptions
        {
            Enabled = true,
            Command = "node",
            Args = [_probe.McpEntryPath],
            WorkingDirectory = Path.GetDirectoryName(_probe.McpEntryPath),
            StartupTimeoutMs = 25_000,
            CallTimeoutMs = 45_000
        });

        _client ??= new SheikhGoMcpStdioClient(opts, NullLogger<SheikhGoMcpStdioClient>.Instance);
        await _client.EnsureReadyAsync(CancellationToken.None);

        var current = new Mock<ICurrentUserService>();
        current.SetupGet(c => c.IsPlatformSuperAdmin).Returns(isSuperAdmin);
        current.SetupGet(c => c.UserId).Returns(42);
        current.Setup(c => c.HasPermission(It.IsAny<string>())).Returns(true);

        var logger = new Mock<IMcpToolExecutionLogger>();
        logger.Setup(l => l.LogAsync(It.IsAny<McpToolExecutionLogEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new SheikhGoMcpBridge(
            _client,
            opts,
            current.Object,
            logger.Object,
            new McpExecutionAmbient(),
            NullLogger<SheikhGoMcpBridge>.Instance);
    }

    private static int ReadCount(string content)
    {
        using var doc = JsonDocument.Parse(ExtractJson(content));
        if (doc.RootElement.TryGetProperty("count", out var c))
            return c.GetInt32();
        throw new InvalidOperationException("MCP content missing count: " + Truncate(content));
    }

    private static int ReadTenantId(string content)
    {
        using var doc = JsonDocument.Parse(ExtractJson(content));
        if (doc.RootElement.TryGetProperty("tenantId", out var t))
            return t.GetInt32();
        throw new InvalidOperationException("MCP content missing tenantId: " + Truncate(content));
    }

    private static string ExtractJson(string content)
    {
        var s = content.Trim();
        // MCP tools sometimes wrap JSON in text blocks — find first object.
        var start = s.IndexOf('{');
        var end = s.LastIndexOf('}');
        if (start >= 0 && end > start)
            return s[start..(end + 1)];
        return s;
    }

    private static string Truncate(string s)
        => s.Length <= 240 ? s : s[..240] + "…";
}
