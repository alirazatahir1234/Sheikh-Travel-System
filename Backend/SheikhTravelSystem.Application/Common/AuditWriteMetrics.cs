using System.Diagnostics.Metrics;

namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Loud failure signal for fail-open audit writes (SG-07).
/// Wire <c>audit.write.failures</c> to an alarm in App Insights / Prometheus.
/// </summary>
public static class AuditWriteMetrics
{
    public const string MeterName = "SheikhGo.Audit";
    public const string FailureCounterName = "audit.write.failures";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> FailureCounter =
        Meter.CreateCounter<long>(FailureCounterName, unit: "{failure}",
            description: "Audit event write failures (fail-open paths)");

    private static long _totalFailures;

    /// <summary>Process-lifetime failure count (tests / diagnostics).</summary>
    public static long TotalFailures => Interlocked.Read(ref _totalFailures);

    public static void RecordFailure(string eventKey, string reason)
    {
        Interlocked.Increment(ref _totalFailures);
        FailureCounter.Add(1,
            new KeyValuePair<string, object?>("event_key", eventKey),
            new KeyValuePair<string, object?>("reason", reason));
    }
}
