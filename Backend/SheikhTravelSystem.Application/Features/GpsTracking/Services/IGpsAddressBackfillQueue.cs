namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Fire-and-forget entry point for backfilling a human-readable address onto a vehicle's current
/// location when the ingested position didn't already carry one (e.g. Traccar's own geocoder is
/// unconfigured/unreachable). Enqueue must never block position ingestion.
/// </summary>
public interface IGpsAddressBackfillQueue
{
    void Enqueue(int vehicleId, double latitude, double longitude);
}
