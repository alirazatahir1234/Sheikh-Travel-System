namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// SG-07 audit write failure policy.
/// True <see cref="FailClosed"/> paths MUST enroll the audit INSERT in the same DB
/// transaction as the mutating work. <see cref="FailLoud"/> throws at the API boundary
/// but is NOT same-TX — the preceding action may already have committed (see SG-52).
/// Fail-open paths never block the caller but MUST LogError + increment
/// <see cref="AuditWriteMetrics"/>.
/// </summary>
public enum AuditFailPolicy
{
    /// <summary>
    /// Default for CRUD pipeline and login. On write failure: LogError + metric, do not throw.
    /// </summary>
    FailOpenLoud = 0,

    /// <summary>
    /// Same-TX security events (permission change, role assignment). On write failure: throw
    /// so the ambient transaction rolls back.
    /// </summary>
    FailClosed = 1,

    /// <summary>
    /// Throws on audit write failure at the API boundary, but the action is NOT in the same
    /// SQL transaction — so the action can commit without an audit row (SG-52). Used for
    /// cross-tenant reads and subscription changes until they are upgraded.
    /// </summary>
    FailLoud = 2
}
