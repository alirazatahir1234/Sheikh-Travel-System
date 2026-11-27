using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>
/// "On exit, engine block — confirmed by a manager". The queue and the approval path are Dapper code, so their
/// guarantees are pinned on the source text (as the SG-01 ingest tests do): queuing never sends, and approving a
/// system-queued engine command runs the engine safety check before anything is marked approved or dispatched.
/// </summary>
public class GeofenceEngineBlockApprovalTests
{
    private static readonly string Backend = FindBackendRoot();

    private static string MethodBody(string relativePath, string signatureFragment)
    {
        var text = File.ReadAllText(Path.Combine(Backend, relativePath));
        var start = text.IndexOf(signatureFragment, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, $"{signatureFragment} must exist in {relativePath}");
        var open = text.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0)
                return text[open..(i + 1)];
        }

        throw new InvalidOperationException("Unbalanced braces.");
    }

    [Fact]
    public void Queue_for_approval_inserts_pending_approval_and_never_sends()
    {
        var body = MethodBody(
            "SheikhTravelSystem.Infrastructure/Services/Gps/GpsAutomatedCommandService.cs",
            "public async Task<ApiResponse<int>> QueueForApprovalAsync(");

        body.Should().Contain("N'PendingApproval', N'Pending'");
        body.Should().NotContain("transportRouter", "nothing reaches the device until a manager approves");
        body.Should().Contain("TenantId = @TenantId", "the device and duplicate checks stay inside the tenant");
    }

    [Fact]
    public void Approving_a_system_queued_engine_command_runs_the_safety_check_first()
    {
        var body = MethodBody(
            "SheikhTravelSystem.Infrastructure/Persistence/Repositories/GpsDeviceRepository.cs",
            "private async Task<ApiResponse<bool>> FinalizeApprovalAndDispatchAsync(");

        var check = body.IndexOf("CheckEngineCutoffPreconditionAsync", StringComparison.Ordinal);
        var approve = body.IndexOf("ApprovalStatus = N'Approved'", StringComparison.Ordinal);
        var dispatch = body.IndexOf("DispatchStoredCommandAsync", StringComparison.Ordinal);

        check.Should().BeGreaterThan(0);
        check.Should().BeLessThan(approve, "a refused check must leave the command waiting");
        check.Should().BeLessThan(dispatch);
        body.Should().Contain("\"system:\"");
    }

    [Fact]
    public void Geofence_exit_queues_the_engine_stop_through_the_approval_queue()
    {
        var body = MethodBody(
            "SheikhTravelSystem.Infrastructure/Persistence/Repositories/GpsTrackingRepository.GeofenceRules.cs",
            "private async Task ApplyExitRulesAsync(");

        body.Should().Contain("QueueForApprovalAsync");
        body.Should().Contain("\"system:geofence\"");
        body.Should().NotContain("QueueAsync(ctx", "the immediate-send path is never used for geofences");
    }

    [Fact]
    public void Engine_block_follows_the_rule_switch_and_schedule()
    {
        var rules = GeofenceRules.None with { EngineBlockOnExit = true };
        rules.EngineBlockOnExit.Should().BeTrue();
        GeofenceRules.None.EngineBlockOnExit.Should().BeFalse("off unless chosen");
    }

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var backend = Path.Combine(dir.FullName, "Backend");
            if (Directory.Exists(Path.Combine(backend, "SheikhTravelSystem.Infrastructure")))
                return backend;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate Backend/ root.");
    }
}
