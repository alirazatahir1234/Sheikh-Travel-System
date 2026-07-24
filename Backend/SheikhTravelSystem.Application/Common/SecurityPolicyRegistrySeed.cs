namespace SheikhTravelSystem.Application.Common;

/// <summary>Stage 13 Security Center — seeded policy catalog keys.</summary>
public static class SecurityPolicyKeys
{
    public const string PasswordMinLength = "password.min_length";
    public const string PasswordComplexity = "password.complexity";
    public const string PasswordMaxAgeDays = "password.max_age_days";
    public const string PasswordHistoryCount = "password.history_count";

    public const string LockoutMaxAttempts = "lockout.max_attempts";
    public const string LockoutMinutes = "lockout.minutes";

    public const string SessionIdleTimeoutMinutes = "session.idle_timeout_minutes";
    public const string SessionAbsoluteTimeoutMinutes = "session.absolute_timeout_minutes";

    public const string AuditLevel = "audit.level";
    public const string AuditLoginEvents = "audit.login_events";

    public const string IpRestrictEnabled = "ip.restrict_enabled";
    public const string IpAllowedCidrs = "ip.allowed_cidrs";

    public const string DeviceTrustedOnly = "device.trusted_only";

    public const string ComplianceGdprLogging = "compliance.gdpr_logging";
    public const string ComplianceDataRetentionDays = "compliance.data_retention_days";
    public const string ComplianceMfaRequired = "compliance.mfa_required";
    public const string ComplianceVatEnabled = "compliance.vat_enabled";
}

public record SecurityPolicySeed(
    string PolicyKey,
    string DisplayName,
    string Category,
    string? Description,
    string DefaultValue,
    string ValueType,
    int SortOrder,
    bool Visible = true);

public static class SecurityPolicyRegistrySeed
{
    public static IReadOnlyList<SecurityPolicySeed> All { get; } =
    [
        new(SecurityPolicyKeys.PasswordMinLength, "Minimum password length", "Password",
            "Minimum characters required for new passwords.", "8", "Int", 10),
        new(SecurityPolicyKeys.PasswordComplexity, "Password complexity", "Password",
            "Require mixed case, digit, and symbol when true.", "false", "Bool", 20),
        new(SecurityPolicyKeys.PasswordMaxAgeDays, "Password max age (days)", "Password",
            "Soft-block login when password age exceeds this. 0 = disabled.", "0", "Int", 30),
        new(SecurityPolicyKeys.PasswordHistoryCount, "Password history count", "Password",
            "Reserved for future history checks. Stored only in foundation.", "0", "Int", 40),

        new(SecurityPolicyKeys.LockoutMaxAttempts, "Max failed login attempts", "Lockout",
            "Lock account after this many failures. 0 = disabled.", "5", "Int", 10),
        new(SecurityPolicyKeys.LockoutMinutes, "Lockout duration (minutes)", "Lockout",
            "How long the account stays locked.", "15", "Int", 20),

        new(SecurityPolicyKeys.SessionIdleTimeoutMinutes, "Idle session timeout (minutes)", "Session",
            "ERP idle logout when greater than 0.", "30", "Int", 10),
        new(SecurityPolicyKeys.SessionAbsoluteTimeoutMinutes, "Absolute session timeout (minutes)", "Session",
            "ERP absolute session ceiling from login. 0 = disabled.", "480", "Int", 20),

        new(SecurityPolicyKeys.AuditLevel, "Audit level", "Audit",
            "Always | Errors | Critical | Disabled", "Always", "String", 10),
        new(SecurityPolicyKeys.AuditLoginEvents, "Audit login events", "Audit",
            "Record login success/failure when audit is enabled.", "true", "Bool", 20),

        new(SecurityPolicyKeys.IpRestrictEnabled, "Restrict by IP", "IP",
            "When true, soft-check client IP against allowed CIDRs on login.", "false", "Bool", 10),
        new(SecurityPolicyKeys.IpAllowedCidrs, "Allowed CIDRs", "IP",
            "One IP or CIDR per line. Soft allowlist only — not a firewall.", "", "StringList", 20),

        new(SecurityPolicyKeys.DeviceTrustedOnly, "Trusted devices only", "Device",
            "Future metadata only. No device enrollment in foundation.", "false", "Bool", 10),

        new(SecurityPolicyKeys.ComplianceGdprLogging, "GDPR logging", "Compliance",
            "Company compliance flag (maps to legacy IsGdprEnabled).", "true", "Bool", 10),
        new(SecurityPolicyKeys.ComplianceDataRetentionDays, "Data retention (days)", "Compliance",
            "Advisory retention hint for operators.", "365", "Int", 20),
        new(SecurityPolicyKeys.ComplianceMfaRequired, "MFA required", "Compliance",
            "Flag only in foundation — no MFA challenge product.", "false", "Bool", 30),
        new(SecurityPolicyKeys.ComplianceVatEnabled, "VAT enabled", "Compliance",
            "Maps to legacy IsVatEnabled.", "false", "Bool", 40),
    ];
}
