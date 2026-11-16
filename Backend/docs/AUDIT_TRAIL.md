# Audit trail (SG-07)

## TargetTenantId semantics (write model A)

| Scenario | `TenantId` | `TargetTenantId` | Payload |
|----------|------------|------------------|---------|
| One tenant’s data read/mutated (e.g. company 7) | **7** (subject/home for retention + tenant search) | **7** | optional details |
| Aggregate / all-companies list | **1** (`PlatformTenant.Id`) | **NULL** | `count` + `tenantIds` — **one row**, never N rows |
| Login (known user) | user’s tenant | user’s tenant | — |
| Login (unknown user) | platform `1` | **NULL** | — |

**NULL means “platform aggregate / no single target”.** It must never mean “forgot to set”.

Rejected option C: burying targets only in JSON under `TenantId = 1` for single-tenant reads.

Tenant-scoped `SearchAsync` matches `TenantId = X OR TargetTenantId = X` so the company whose data was read can see the event.

## Fail policy

| Category | Policy | Same DB transaction? |
|----------|--------|----------------------|
| Permission change (`roles.permissions`) | **Fail-closed** | **Yes** — replace + audit in one TX |
| Role assignment (`roles.assignment`) | **Fail-closed** | **Yes** — replace + audit in one TX |
| Cross-tenant read | **Fail-loud** (throws at API boundary) | **No** — SELECT then audit INSERT; response withheld if audit throws, but the read already happened (SG-52) |
| PII reveal (`privacy.pii.reveal`) | Fail-closed when enrolled via `IPiiRevealAudit` | Callers must enroll in the same TX; no reveal endpoint yet |
| Subscription/billing change | **Fail-loud** (throws at API boundary) | **No** — mutate then audit; audit failure throws after mutation may already be committed (SG-52; can go same-TX later) |
| Impersonation | Fail-closed (key reserved; same TX required) | No endpoint yet (`Platform.Tenants.Impersonate` seeded) |
| Login success / failure | Fail-open + **loud** (`LogError` + `audit.write.failures`) | N/A |
| Normal CRUD (`IAuditableCommand` pipeline) | Fail-open + loud | N/A |

**Fail-closed** = same SQL transaction; action rolls back with the audit INSERT.
**Fail-loud** = throw at the API boundary so the caller does not see a clean success, but the preceding action can still commit without an audit row. Do not call that fail-closed.

Silent `LogDebug` on audit failure is banned.

### SG-52 gap (honest)

Cross-tenant read and subscription change are labelled **fail-loud**, not fail-closed.
Future fixes (not implemented yet):

- **Reads:** write + commit the audit row first, then perform the read (only practical fail-closed path for reads).
- **Subscription:** can move into the same SQL transaction as `ApplySubscriptionActionAsync`.

## Read path

- `Platform.Tenants.AuditReadAll` + no concrete tenant → all-companies search (`AllCompanies=true`), no throw for missing tenant.
- ERP Audit Center uses SG-06 `isAllCompanies` and omits `tenantId` in that mode.
- Canonical route: `/platform/audit-center` (SG-36). `/audit-logs` redirects.

## Append-only

Triggers `TR_AuditEvents_NoUpdate` / `TR_AuditEvents_NoDelete` reject UPDATE/DELETE.
