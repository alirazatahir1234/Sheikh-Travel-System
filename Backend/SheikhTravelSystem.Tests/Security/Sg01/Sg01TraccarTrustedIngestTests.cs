using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.API.Controllers;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Infrastructure.Services.Gps;
using SheikhTravelSystem.Infrastructure.Traccar;

namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 Step 6a, review finding H1 — Traccar positions are trusted because of server-only provenance
/// (<see cref="TraccarIngestProvenance"/>), not because no HTTP request is present. Manual sync runs inside an
/// authenticated request and must use the same trusted path as the background loop; a rejected position must not
/// be counted or advance the watermark, so it is retried.
/// </summary>
public class Sg01TraccarTrustedIngestTests
{
    private static readonly string Backend = FindBackendRoot();

    private static IngestPositionDto Position() =>
        new(VehicleId: 10, DriverId: null, BookingId: null, GpsDeviceId: 3,
            Latitude: 1, Longitude: 1, Speed: 0m, Ignition: true);

    private static Mock<IMediator> Mediator(bool accept, Action? onSend = null)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<IngestPositionCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => onSend?.Invoke())
            .ReturnsAsync(accept
                ? ApiResponse<bool>.SuccessResponse(true)
                : ApiResponse<bool>.FailResponse(GpsIngestScopePolicy.ScopeRejection));
        return mediator;
    }

    // ---- trusted provenance ------------------------------------------------------------------

    [Fact]
    public async Task Traccar_ingest_runs_inside_the_trusted_provenance_scope_and_releases_it()
    {
        var seenInsideSend = false;
        var mediator = Mediator(accept: true, onSend: () => seenInsideSend = TraccarIngestProvenance.IsActive);

        TraccarIngestProvenance.IsActive.Should().BeFalse();
        await TraccarTrustedIngest.SendAsync(mediator.Object, Position(), new PositionIngestSourceContext(), CancellationToken.None);

        seenInsideSend.Should().BeTrue("ingest must see the trusted Traccar provenance");
        TraccarIngestProvenance.IsActive.Should().BeFalse("the scope must not leak past the send");
    }

    [Fact]
    public void Trusted_provenance_wins_even_when_an_authenticated_user_is_present()
    {
        // Manual sync: the orchestrator runs inside the platform operator's HTTP request.
        GpsIngestScopePolicy.Classify(userId: 42, driverId: null, isTrustedTraccarIngest: true)
            .Should().Be(GpsIngestCaller.TrustedSystem);

        var decision = GpsIngestScopePolicy.Decide(
            GpsIngestCaller.TrustedSystem, callerTenantId: 1,
            new GpsIngestFacts(VehicleTenantId: 7, DeviceOk: true, BookingOk: true, DriverOk: true, DriverHasStartedBooking: false));
        decision.Allowed.Should().BeTrue();
        decision.TenantId.Should().Be(7, "trusted Traccar data is written for the vehicle's own company");
    }

    [Fact]
    public void Trusted_path_still_enforces_device_and_vehicle_ownership()
        => GpsIngestScopePolicy.Decide(
                GpsIngestCaller.TrustedSystem, null,
                new GpsIngestFacts(VehicleTenantId: 7, DeviceOk: false, BookingOk: true, DriverOk: true, DriverHasStartedBooking: false))
            .Rejection.Should().Be(GpsIngestScopePolicy.ScopeRejection);

    [Fact]
    public void Outside_the_scope_an_authenticated_company_user_stays_tenant_restricted()
    {
        var caller = GpsIngestScopePolicy.Classify(userId: 42, driverId: null, isTrustedTraccarIngest: TraccarIngestProvenance.IsActive);
        caller.Should().Be(GpsIngestCaller.User);

        GpsIngestScopePolicy.Decide(caller, callerTenantId: 1,
                new GpsIngestFacts(VehicleTenantId: 2, DeviceOk: true, BookingOk: true, DriverOk: true, DriverHasStartedBooking: false))
            .Rejection.Should().Be(GpsIngestScopePolicy.ScopeRejection);
    }

    [Fact]
    public void Anonymous_caller_cannot_claim_trusted_provenance()
    {
        GpsIngestScopePolicy.Classify(userId: null, driverId: null, isTrustedTraccarIngest: TraccarIngestProvenance.IsActive)
            .Should().Be(GpsIngestCaller.Unauthenticated);

        typeof(TraccarIngestProvenance).IsPublic.Should().BeFalse("only Infrastructure code may open the scope");
        typeof(TraccarIngestProvenance).GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(m => m.DeclaringType == typeof(TraccarIngestProvenance))
            .Should().BeEmpty();
    }

    [Fact]
    public void No_API_action_can_bind_ingest_provenance_or_the_ingest_command()
    {
        var forbidden = new[] { typeof(PositionIngestSourceContext), typeof(IngestPositionCommand) };
        var offenders = new List<string>();

        foreach (var controller in typeof(GpsTrackingController).Assembly.GetTypes()
                     .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var parameter in action.GetParameters())
                {
                    if (parameter.GetCustomAttribute<FromServicesAttribute>() is not null)
                        continue;
                    if (Reaches(parameter.ParameterType, forbidden, new HashSet<Type>(), depth: 0))
                        offenders.Add($"{controller.Name}.{action.Name}({parameter.Name})");
                }
            }
        }

        offenders.Should().BeEmpty("trusted provenance must never come from model binding");
    }

    // ---- manual + background sync use the same trusted path -----------------------------------

    [Fact]
    public void Every_Traccar_ingest_goes_through_the_trusted_helper()
    {
        var orchestrator = Source("TraccarSyncOrchestrator.cs");

        orchestrator.Should().NotContain("new IngestPositionCommand", "positions may only be sent via TraccarTrustedIngest");
        Regex.Matches(orchestrator, @"TraccarTrustedIngest\.SendAsync\(").Count.Should().Be(2,
            "tracker sync and position sync (background + manual) both use the trusted helper");

        Normalize(MethodBody(orchestrator, "RunManualSyncAsync"))
            .Should().Contain("await SyncPositionsAsync(ct)", "manual sync reuses the background position path");
        Normalize(Source("TraccarSyncService.cs")).Should().Contain("o.SyncPositionsAsync(stoppingToken)");
    }

    [Theory]
    [InlineData("TrackersSyncAll", "trackers/sync-all")]
    [InlineData("TraccarSync", "traccar/sync")]
    public void Platform_wide_manual_sync_requires_a_platform_permission(string _, string route)
    {
        var action = typeof(GpsTrackingController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.GetCustomAttributes<HttpPostAttribute>().Any(a => a.Template == route));

        var policies = action.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Policy).ToList();
        policies.Should().Contain(PlatformPermissions.GpsGatewaysManage);
        policies.Should().NotContain(GpsPermissions.DeviceManage,
            "a company permission must not start a sync that ingests every company's positions");
        PlatformPermissions.IsPlatformScopePermission(PlatformPermissions.GpsGatewaysManage).Should().BeTrue(
            "platform-scope permissions are only honoured on a platform-tenant token");
    }

    // ---- result handling: rejected positions are retried --------------------------------------

    [Fact]
    public async Task Rejected_position_is_reported_as_not_ingested()
        => (await TraccarTrustedIngest.SendAsync(Mediator(accept: false).Object, Position(), new PositionIngestSourceContext(), CancellationToken.None))
            .Should().BeFalse();

    [Fact]
    public async Task Accepted_position_is_reported_as_ingested()
        => (await TraccarTrustedIngest.SendAsync(Mediator(accept: true).Object, Position(), new PositionIngestSourceContext(), CancellationToken.None))
            .Should().BeTrue();

    [Fact]
    public void Rejected_position_does_not_advance_the_watermark_and_is_retried()
    {
        var watermark = new TraccarPositionWatermark();
        var fix = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

        // First sync: ingest rejected -> nothing marked.
        watermark.AlreadyProcessed(5, fix).Should().BeFalse();

        // Next sync: the same fix is still eligible, and an accepted retry marks it.
        watermark.AlreadyProcessed(5, fix).Should().BeFalse("a rejected fix must be retried");
        watermark.MarkProcessed(5, fix);
        watermark.AlreadyProcessed(5, fix).Should().BeTrue("an ingested fix is not re-ingested");
        watermark.AlreadyProcessed(5, fix.AddSeconds(-1)).Should().BeTrue();
        watermark.AlreadyProcessed(5, fix.AddSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void Position_sync_counts_and_advances_only_accepted_positions()
    {
        var body = Normalize(MethodBody(Source("TraccarSyncOrchestrator.cs"), "SyncPositionsAsync"));

        body.Should().Contain(
            "if (await TraccarTrustedIngest.SendAsync(mediator, dto, source, ct)) { await RefreshConnectivityAsync( connection, linked.GpsDeviceId, linked.VehicleId, seenAt, ct); ingested++; _watermark.MarkProcessed(pos.DeviceId, pos.FixTime); } else {");
        var acceptedBranchEnd = body.IndexOf("_watermark.MarkProcessed(pos.DeviceId, pos.FixTime); } else {", StringComparison.Ordinal);
        acceptedBranchEnd.Should().BeGreaterThan(0);
        var rejectedStart = body.IndexOf("else {", acceptedBranchEnd, StringComparison.Ordinal) + "else {".Length;
        var rejectedBranch = body[rejectedStart..body.IndexOf('}', rejectedStart)];
        rejectedBranch.Should().Contain("rejected++;");
        rejectedBranch.Should().NotContain("ingested++", "a rejected position is not counted as ingested");
        rejectedBranch.Should().NotContain("MarkProcessed", "a rejected position must not advance the watermark");
        body.Should().NotContain("finally { // Always advance the watermark",
            "the unconditional watermark advance is what lost rejected positions");
        body.Should().Contain("new TraccarSyncJobResult(\"positions\", positions.Count, ingested, telemetryOnly + heartbeats, rejected + failed + failedPermanently)");
    }

    // ---- result handling: a thrown ingest is retried a bounded number of times -----------------

    [Fact]
    public void Failed_position_is_retried_until_the_attempt_budget_is_exhausted()
    {
        var watermark = new TraccarPositionWatermark();
        var fix = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

        for (var attempt = 1; attempt < TraccarPositionWatermark.MaxFailedAttempts; attempt++)
        {
            watermark.RecordFailureAndShouldGiveUp(5, fix).Should().BeFalse($"attempt {attempt} is within budget");
            watermark.AlreadyProcessed(5, fix).Should().BeFalse("a transient failure must be retried, not lost");
        }

        watermark.RecordFailureAndShouldGiveUp(5, fix).Should().BeTrue("the last attempt gives up");
        watermark.AlreadyProcessed(5, fix).Should().BeTrue("a poison position must not block the device forever");
    }

    [Fact]
    public void Failed_then_successful_retry_resets_the_attempt_budget()
    {
        var watermark = new TraccarPositionWatermark();
        var fix = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

        watermark.RecordFailureAndShouldGiveUp(5, fix).Should().BeFalse();
        watermark.MarkProcessed(5, fix); // retry succeeded

        var next = fix.AddMinutes(1);
        for (var attempt = 1; attempt < TraccarPositionWatermark.MaxFailedAttempts; attempt++)
            watermark.RecordFailureAndShouldGiveUp(5, next).Should().BeFalse("a new fix gets its own full budget");
    }

    [Fact]
    public void Failure_budget_is_per_device()
    {
        var watermark = new TraccarPositionWatermark();
        var fix = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

        for (var attempt = 1; attempt < TraccarPositionWatermark.MaxFailedAttempts; attempt++)
            watermark.RecordFailureAndShouldGiveUp(5, fix);

        watermark.RecordFailureAndShouldGiveUp(6, fix).Should().BeFalse();
        watermark.AlreadyProcessed(6, fix).Should().BeFalse();
    }

    [Fact]
    public void Position_sync_exception_branch_does_not_advance_the_watermark_directly()
    {
        var body = Normalize(MethodBody(Source("TraccarSyncOrchestrator.cs"), "SyncPositionsAsync"));

        var start = body.IndexOf("_watermark.RecordFailureAndShouldGiveUp(pos.DeviceId, pos.FixTime)", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "the ingest catch must go through the bounded-retry budget");
        var catchStart = body.LastIndexOf("catch (Exception ex) {", start, StringComparison.Ordinal);
        catchStart.Should().BeGreaterThan(0);
        var catchBody = body[catchStart..start];
        catchBody.Should().NotContain("MarkProcessed", "a thrown ingest must not advance the watermark unconditionally");
        body.Should().NotContain("an exception still advances the watermark");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static bool Reaches(Type type, Type[] forbidden, HashSet<Type> seen, int depth)
    {
        if (depth > 5 || !seen.Add(type))
            return false;
        if (forbidden.Contains(type))
            return true;
        if (type.IsPrimitive || type == typeof(string) || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true
            && !type.IsGenericType && !type.IsArray)
            return false;

        if (type.IsArray && Reaches(type.GetElementType()!, forbidden, seen, depth + 1))
            return true;
        foreach (var arg in type.IsGenericType ? type.GetGenericArguments() : [])
            if (Reaches(arg, forbidden, seen, depth + 1))
                return true;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (Reaches(property.PropertyType, forbidden, seen, depth + 1))
                return true;
        return false;
    }

    private static string Source(string fileName)
        => File.ReadAllText(Directory.EnumerateFiles(Backend, fileName, SearchOption.AllDirectories)
            .First(p => !p.Contains("/obj/") && !p.Contains("/bin/")));

    private static string MethodBody(string text, string methodName)
    {
        var declaration = Regex.Match(text, $@"\bTask\b[^;=]*?\s{Regex.Escape(methodName)}\s*\(");
        declaration.Success.Should().BeTrue(because: $"{methodName} must still exist");
        var open = text.IndexOf('{', declaration.Index);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0)
                return text[open..(i + 1)];
        }
        throw new InvalidOperationException($"Unbalanced braces in {methodName}");
    }

    private static string Normalize(string s) => Regex.Replace(s, @"\s+", " ");

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
