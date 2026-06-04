namespace SheikhTravelSystem.Application.Features.CustomerPortal;

public class PortalPaymentGatewaySettings
{
    public const string SectionName = "PortalPaymentGateway";

    public bool Enabled { get; set; }

    public string Provider { get; set; } = "Stripe";
}
