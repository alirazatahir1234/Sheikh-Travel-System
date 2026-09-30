namespace SheikhTravelSystem.Application.Features.CustomerPortal;

/// <summary>Configuration for customer portal OTP and JWT.</summary>
public class PortalAuthSettings
{
    public const string SectionName = "PortalAuth";

    public int OtpExpiryMinutes { get; set; } = 10;

    public int PortalTokenExpiryMinutes { get; set; } = 10_080;

    /// <summary>
    /// When true <em>and</em> the host is Development, OTP is fixed to <see cref="DevOtpCode"/> (no SMS).
    /// Ignored in Staging/Production even if set in configuration.
    /// </summary>
    public bool DevMode { get; set; }

    /// <summary>Fixed OTP used only when Development + <see cref="DevMode"/>.</summary>
    public string DevOtpCode { get; set; } = string.Empty;
}
