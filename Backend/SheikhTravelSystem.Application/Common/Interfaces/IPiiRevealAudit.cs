namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// SG-07 / SG-18 hook: every PII reveal (e.g. CNIC unmask) MUST write a fail-closed audit event.
/// No reveal endpoint exists yet — implementers call <see cref="RecordRevealAsync"/> in the same
/// DB transaction as the reveal response payload is prepared.
/// </summary>
public interface IPiiRevealAudit
{
    /// <summary>
    /// Records <c>privacy.pii.reveal</c> with TenantId = subject company, TargetTenantId = subject company.
    /// Fail-closed: throws when the audit INSERT fails.
    /// </summary>
    Task RecordRevealAsync(
        int subjectTenantId,
        string entityType,
        int entityId,
        string fieldName,
        CancellationToken cancellationToken = default);
}
