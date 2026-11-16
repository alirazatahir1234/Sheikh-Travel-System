namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// SG-07 audit write failure policy.
/// Fail-closed paths MUST enroll the audit INSERT in the same DB transaction as the
/// mutating work (or withhold the response for pure reads). Fail-open paths never
/// block the caller but MUST LogError + increment <see cref="AuditWriteMetrics"/>.
/// </summary>
public enum AuditFailPolicy
{
    /// <summary>
    /// Default for CRUD pipeline and login. On write failure: LogError + metric, do not throw.
    /// </summary>
    FailOpenLoud = 0,

    /// <summary>
    /// Security-sensitive categories. On write failure: throw so the ambient transaction
    /// rolls back (or the handler aborts before returning data).
    /// </summary>
    FailClosed = 1
}
