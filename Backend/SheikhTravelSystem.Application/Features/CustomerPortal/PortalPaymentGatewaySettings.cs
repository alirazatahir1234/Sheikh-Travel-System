namespace SheikhTravelSystem.Application.Features.CustomerPortal;

public class PortalPaymentGatewaySettings
{
    public const string SectionName = "PortalPaymentGateway";

    public bool Enabled { get; set; }

    public string Provider { get; set; } = "Stripe";

    public string ReturnBaseUrl { get; set; } = "http://localhost:4300";

    public string ApiBaseUrl { get; set; } = "http://localhost:5082";
}
