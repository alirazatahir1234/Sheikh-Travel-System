using System.Collections.Concurrent;

namespace SheikhTravelSystem.Infrastructure.SignalR;

/// <summary>Lightweight connection counter for health/metrics endpoints.</summary>
public static class TrackingHubMetrics
{
    private static int _connectedClients;

    public static int ConnectedClients => Volatile.Read(ref _connectedClients);

    internal static void Increment() => Interlocked.Increment(ref _connectedClients);

    internal static void Decrement()
    {
        if (_connectedClients > 0)
            Interlocked.Decrement(ref _connectedClients);
    }
}
