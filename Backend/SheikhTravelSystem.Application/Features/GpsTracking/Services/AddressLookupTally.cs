namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>Places a History replay named from the address cache, and places it queued for a background lookup.</summary>
public sealed class AddressLookupTally
{
    public int Named { get; set; }
    public int Queued { get; set; }
}
