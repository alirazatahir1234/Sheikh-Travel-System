namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Third-party driver identity and background screening (NADRA / UAE KYC).
/// Secrets via user-secrets or env — see Backend/docs/DRIVER_VERIFICATION_PROVIDERS.md.
/// </summary>
public sealed class DriverVerificationOptions
{
    public const string SectionName = "DriverVerification";

    public NadraVerificationOptions Nadra { get; set; } = new();
    public UaeKycVerificationOptions UaeKyc { get; set; } = new();

    /// <summary>Days before expiry to notify drivers (license + compliance docs).</summary>
    public int ExpiryAlertDays { get; set; } = 30;
}

public sealed class NadraVerificationOptions
{
    /// <summary>Nishan | Verisys | ESahulat</summary>
    public string Channel { get; set; } = "Nishan";

    public bool Enabled { get; set; }

    /// <summary>API base URL from NADRA Nishaan / franchise contract (no default — must be configured).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Relative path for CNIC demographic / Verisys call.</summary>
    public string VerisysPath { get; set; } = "/api/verisys/demographic";

    /// <summary>Nishaan Pakistan: company / client identifier.</summary>
    public string CompanyId { get; set; } = "";

    /// <summary>Nishaan Pakistan: API key from portal onboarding.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>e-Sahulat franchise identifier.</summary>
    public string FranchiseId { get; set; } = "";

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>e-Sahulat CNIC verify path (from franchise integration pack).</summary>
    public string ESahulatVerifyPath { get; set; } = "/api/cnic/verify";

    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class UaeKycVerificationOptions
{
    /// <summary>Bayzat | Generic — Generic uses configurable screening endpoint.</summary>
    public string Provider { get; set; } = "Bayzat";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://api.bayzat.com";

    public string OAuthTokenPath { get; set; } = "/oauth/token";

    /// <summary>POST path for background / AML screening (from partner contract).</summary>
    public string ScreeningPath { get; set; } = "/v1/compliance/background-screening";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>Bayzat company / tenant id when applicable.</summary>
    public string CompanyId { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 45;
}
