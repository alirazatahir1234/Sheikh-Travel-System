using System.Text.Json;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence;

/// <summary>
/// SG-07 stub for SG-18 PII reveal auditing. Fail-closed; no reveal UI yet.
/// </summary>
public class PiiRevealAudit(IAuditEngine auditEngine) : IPiiRevealAudit
{
    public Task RecordRevealAsync(
        int subjectTenantId,
        string entityType,
        int entityId,
        string fieldName,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            field = fieldName,
            note = "SG-18 reveal path — enroll this call in the same TX as the reveal response."
        });

        return auditEngine.RecordAsync(new AuditEventWrite(
            EventKey: AuditEventKeys.PiiReveal,
            EntityType: entityType,
            EntityId: entityId,
            Action: "RevealPII",
            NewValues: payload,
            Success: true,
            Message: $"PII field '{fieldName}' revealed",
            TenantId: subjectTenantId,
            TargetTenantId: subjectTenantId,
            FailPolicy: AuditFailPolicy.FailClosed), cancellationToken);
    }
}
