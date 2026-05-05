namespace SheikhTravelSystem.Application.Features.CustomerPortal;

public static class PortalPayStateHelper
{
    public static PortalPayState FromAmounts(decimal total, decimal paid)
    {
        if (total <= 0) return PortalPayState.Unpaid;
        var remaining = total - paid;
        if (remaining <= 0) return PortalPayState.Paid;
        if (paid > 0) return PortalPayState.PartiallyPaid;
        return PortalPayState.Unpaid;
    }
}
