namespace SheikhTravelSystem.Application.Features.CustomerPortal;

/// <summary>How the customer intends to pay at booking time (portal flow).</summary>
public enum PortalPaymentPlan
{
    Full = 1,
    Partial = 2,
    PayLater = 3
}

/// <summary>Aggregated payment position for a booking (customer-facing).</summary>
public enum PortalPayState
{
    Paid = 1,
    PartiallyPaid = 2,
    Unpaid = 3
}
