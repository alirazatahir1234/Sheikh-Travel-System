using SheikhTravelSystem.Domain.Common;

namespace SheikhTravelSystem.Domain.Entities;

/// <summary>
/// WhatsApp Business phone line (multi-account per tenant). Access tokens are never stored here.
/// </summary>
public class WhatsAppAccount : BaseEntity
{
    /// <summary>Stable config key (e.g. UAE, PK) for WhatsApp:Accounts:{Code}.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string DisplayPhoneNumber { get; set; } = string.Empty;
    public string? PhoneNumberId { get; set; }
    public string? BusinessAccountId { get; set; }
    public string? CountryCode { get; set; }
    public string? Country { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "Active";

    /// <summary>UTC timestamp of the last admin health check (Graph credential ping).</summary>
    public DateTime? LastHealthCheckedAtUtc { get; set; }

    /// <summary>Healthy | Unhealthy | Misconfigured</summary>
    public string? LastHealthStatus { get; set; }

    public string? LastHealthMessage { get; set; }
}
