namespace SheikhTravelSystem.Application.Features.CustomerPortal;

/// <summary>Configuration for customer portal OTP and JWT.</summary>
public class PortalAuthSettings
{
    public const string SectionName = "PortalAuth";

    public int OtpExpiryMinutes { get; set; } = 10;

    public int PortalTokenExpiryMinutes { get; set; } = 10_080;

    /// <summary>When true, OTP is fixed to <see cref="DevOtpCode"/> (no SMS).</summary>
    public bool DevMode { get; set; } = true;

    public string DevOtpCode { get; set; } = "123456";
}
