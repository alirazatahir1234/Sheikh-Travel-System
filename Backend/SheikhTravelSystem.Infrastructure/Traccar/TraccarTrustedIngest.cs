using System.Collections.Concurrent;
using MediatR;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Traccar;

/// <summary>
/// SG-01 Step 6a (H1) — server-only provenance for positions that come from the Traccar server.
/// It is an ambient flag, not a DTO field, header or query value, so no HTTP client can set it; and it is
/// <c>internal</c>, so only Infrastructure code can open it. GPS ingest treats a position as trusted Traccar data
/// only while this scope is active — whether the sync runs in the background loop or inside a manual-sync request.
/// </summary>
internal static class TraccarIngestProvenance
{
    private static readonly AsyncLocal<bool> Active = new();

    internal static bool IsActive => Active.Value;

    internal static IDisposable Begin()
    {
        var previous = Active.Value;
        Active.Value = true;
        return new Restore(previous);
    }

    private sealed class Restore(bool previous) : IDisposable
    {
        public void Dispose() => Active.Value = previous;
    }
}

/// <summary>The only way Traccar sync code submits a position: inside the trusted provenance scope.</summary>
internal static class TraccarTrustedIngest
{
    /// <returns><c>true</c> only when ingest accepted the position.</returns>
    internal static async Task<bool> SendAsync(
        IMediator mediator,
        IngestPositionDto dto,
        PositionIngestSourceContext source,
        CancellationToken ct)
    {
        using (TraccarIngestProvenance.Begin())
        {
            var result = await mediator.Send(new IngestPositionCommand(dto, source), ct);
            return result.Success;
        }
    }
}

/// <summary>
/// Per-Traccar-device "last processed fix" watermark shared by background and manual sync.
/// A rejected position must NOT advance it, so the next sync retries the same fix.
/// A position whose ingest throws is retried up to <see cref="MaxFailedAttempts"/> times (transient DB / timeout
/// failures recover on a later poll); only then is it given up so a poison position cannot block the device forever.
/// State is in memory: a restart clears it, which re-processes the latest fix rather than losing it.
/// ACCEPTED LIMITATION (not a tenant-isolation issue): Traccar supplies only the latest fix per device, so a fix
/// that is given up after <see cref="MaxFailedAttempts"/> or superseded by a newer accepted fix is not stored in
/// history. Durable retry / dead-letter processing is a possible future improvement.
/// </summary>
internal sealed class TraccarPositionWatermark
{
    internal const int MaxFailedAttempts = 3;

    private readonly ConcurrentDictionary<int, DateTime> _lastProcessed = new();
    private readonly ConcurrentDictionary<(int DeviceId, DateTime FixTime), int> _failedAttempts = new();

    /// <summary>True when this fix was already processed (heartbeat / duplicate).</summary>
    internal bool AlreadyProcessed(int traccarDeviceId, DateTime fixTime)
        => _lastProcessed.TryGetValue(traccarDeviceId, out var last) && fixTime <= last;

    internal void MarkProcessed(int traccarDeviceId, DateTime fixTime)
    {
        _lastProcessed[traccarDeviceId] = fixTime;
        foreach (var key in _failedAttempts.Keys)
            if (key.DeviceId == traccarDeviceId && key.FixTime <= fixTime)
                _failedAttempts.TryRemove(key, out _);
    }

    /// <summary>
    /// Records a failed (thrown) ingest attempt. Returns <c>true</c> when the attempt budget is exhausted;
    /// the fix is then marked processed (given up). Otherwise the watermark is left alone so the fix is retried.
    /// </summary>
    internal bool RecordFailureAndShouldGiveUp(int traccarDeviceId, DateTime fixTime)
    {
        var attempts = _failedAttempts.AddOrUpdate((traccarDeviceId, fixTime), 1, (_, n) => n + 1);
        if (attempts < MaxFailedAttempts)
            return false;

        MarkProcessed(traccarDeviceId, fixTime);
        return true;
    }
}
