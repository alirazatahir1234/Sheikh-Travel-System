namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Places a request could not name from the address cache, looked up later in the background at the
/// free geocoder's pace. The answer lands in GpsAddressCache, so a later load shows the name.
/// Enqueue never blocks and never waits for the lookup.
/// </summary>
public interface IGpsAddressLookupQueue
{
    void Enqueue(double latitude, double longitude);
}
