using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Travelers;

/// <summary>Applies computed passport status and PII masking before a traveler leaves the API.</summary>
public static class TravelTravelerPresenter
{
    public static TravelTravelerDto Present(TravelTravelerDto t, bool canViewPii, DateTime todayUtc, int warningDays)
    {
        t.PassportStatus = TravelPii.PassportStatus(t.PassportNumber, t.PassportExpiryDate, todayUtc, warningDays);
        if (!canViewPii)
        {
            t.PiiMasked = t.PassportNumber is not null || t.Cnic is not null;
            t.PassportNumber = TravelPii.Mask(t.PassportNumber);
            t.Cnic = TravelPii.Mask(t.Cnic, 4);
        }
        return t;
    }
}
