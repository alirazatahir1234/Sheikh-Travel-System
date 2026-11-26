# SheikhGo TravelNexus — Phase 8 security review

Scope: the TravelNexus module (Backend `Features/TravelNexus`, `Repositories/TravelNexus`, `Controllers/TravelNexus`,
ERP `features/travel-nexus`) plus the shared-core code it depends on. Review date: November 2026. The audit rules that
bind the module are in `Backend/CLAUDE.md` (tenant isolation, authorization by permission, PII, money) and the issues
in `docs/audit/audit.md`.

Status values: **Fixed (Phase 8)** — changed in the Phase 8 commits; **Reported** — outside this module, handed to
the owner with no code change; **Accepted** — known, mitigated, documented.

## 1. Findings

| # | Severity | Where | Finding | Status |
|---|---|---|---|---|
| TN-01 | High | ERP `core/services/auth.service.ts` `hasPermission` / `hasAnyPermission` | A user whose role was named `SUPER_ADMIN` (any spelling) passed every permission check in the UI regardless of the permissions in the token. The API never bypassed, so this showed menus and buttons the API then refused. Authorization must be by permission only. | Fixed (Phase 8) — role-name branch removed; `permission.guard.ts` no longer lists the super-admin role in its AI-route allowance; specs added. Related: SG-05, SG-51. |
| TN-02 | High | `TravelGroupMemberCommands.CancelTravelGroupMemberCommandHandler` | Cancelling a pilgrim committed the member row first and updated the group visa case in a second transaction; a failure between the two left a cancelled pilgrim still applying for a visa, with only a message telling the agent to fix it by hand. | Fixed (Phase 8) — one transaction (`TravelGroupRepository.CancelMemberAsync` with `TravelVisaWithdrawal`); a visa case that moved meanwhile rolls the cancellation back. Live test `UmrahGroupWorkflowLiveSqlTests`. |
| TN-03 | High | `TravelCommissionRepository.RecordPayoutAsync` | The payable-balance check ran in the handler before the insert; two concurrent payouts could both pass and together exceed the payable commission. | Fixed (Phase 8) — `sp_getapplock` per beneficiary inside the transaction and a re-check of `paid + amount ≤ payable`. Live test `Two_concurrent_payouts_at_the_cap_let_exactly_one_through`. |
| TN-04 | Medium | `TravelDocumentsController` upload, `TravelPassportController` scan | No API-level rate limit on the two most expensive endpoints (10 MB uploads, Azure Document Intelligence calls); only nginx's 120 requests/minute applied. | Fixed (Phase 8) — `[EnableRateLimiting("general")]` (60 requests/minute per IP outside Development). |
| TN-05 | Medium | `TravelProfitabilityService.ComputeAsync` | Every attributed invoice of the tenant was loaded and filtered in memory for each profitability computation (dashboard profit card, reports, context drill-down). Correct but O(all invoices) per request — a slow-down vector for a large tenant. | Fixed (Phase 8) — `GetAttributedInvoicesByIdsAsync` loads only the invoices of the contexts in hand. |
| TN-06 | Medium | `TravelNexusTenantGuardTests` | The static tenant guard scanned only `"""` raw literals; one-line interpolated statements, SQL fragment constants interpolated into many statements, and repository methods that take a branch scope but ignore it were not checked. | Fixed (Phase 8) — three new facts (interpolated statements, fragment JOINs, branch-scope use). No offender was found in the current code. |
| TN-07 | Medium | ERP `core/interceptors/api-envelope.interceptor.ts` | The envelope's `message` was dropped on success, so the UI could not show what the server did (visa withdrawal, "ticket still held", idempotent replays); envelopes serialised without `data` were not recognised and reached components as raw objects. | Fixed (Phase 8) — message kept as `X-Api-Message` (`apiMessageOf`), message-only envelopes recognised, failure `statusText` carries the server text. Pilgrim-cancel toast now shows the server message. |
| TN-08 | Medium | Role management | Companies provisioned after the TravelNexus migrations received no `Travel.*` permissions when the module was enabled for them (the migrations grant only tenants existing at migration time). | Fixed (Phase 8) — grant-on-module-enable hook (`ModulePermissionGrants`) on the module-set, tenant-update and provisioning paths, plus `TravelNexusModuleGrantBackfillMigration`. Live test `ModuleGrantLiveSqlTests`. |
| TN-09 | High | Working tree | **Secrets present in the repository checkout:** `Backend/SheikhTravelSystem.API/appsettings.Local.json` (production VM address and an `sa` SQL login), `docker-compose.db.yml` and `docker-compose.db.env` (SQL `SA_PASSWORD`). Per the working rules these were **not edited or deleted**: a committed credential must be rotated, and removing the file does not remove it from history. | Reported — rotate the SQL `sa` password on the VM and in the local compose stack, then move the values to user-secrets / host environment (see `deployment.md`) and purge the files. Related: SG-07, SG-29. |
| TN-10 | High | Shared core (`Program.cs` static `/uploads`, `AzureBlobStorageService`) | Nexus uploads (driver CNIC images, vehicle documents, trip files) are publicly readable by URL; Azure read SAS links last 365 days. Documented in `architecture.md` §8 since Phase 3. TravelNexus does not use that path. | Reported — needs its own SG id (the ERP binds these URLs in many `<img>`/`<a>` tags). Related: SG-16, SG-18. |
| TN-11 | Medium | `TravelPassportController` → Azure Document Intelligence | The passport photo page leaves the platform to Microsoft's OCR service (PII egress). Nothing is stored by SheikhGo; the result is only returned to the caller. Opt-in: the endpoint does nothing without `Ocr:AzureEndpoint` / `Ocr:AzureApiKey`. | Accepted — document in the tenant's data-processing terms; keep the keys out of the repository. |
| TN-12 | Low | `TravelDocumentFileValidator` | No antivirus scan on uploaded documents. Mitigations: extension allow-list (pdf, jpg, jpeg, png), magic-byte check that must agree with the extension, 10 MB cap, SHA-256 de-duplication, files only ever streamed back with `no-store` / `nosniff` to users holding `Travel.Document.Download`, never executed or served statically. | Accepted — add a scanner (e.g. Defender for Storage on the private container) when the Azure subscription allows. |
| TN-13 | Low | ERP `core/navigation/menu-config.ts`, `core/services/menu.service.ts` | The platform hub's route allow-list still keys on the `SUPER_ADMIN` role name (which routes a platform operator may *see*, not what the API allows). Out of TravelNexus scope. | Reported — fold into SG-05 / SG-51 when the platform permissions land. |

## 2. What was verified

- **Tenant isolation.** `Sg01TenantSqlGuardScanner` and `TravelNexusTenantGuardTests` (now six facts) pass over every
  TravelNexus repository. Each live end-to-end test ends with company B reading company A's ids and receiving not-found
  (`VisaWorkflowLiveSqlTests`, `UmrahGroupWorkflowLiveSqlTests`, `FinanceWorkflowLiveSqlTests`). The tenant id comes from
  `ITenantContext` in every handler; no TravelNexus endpoint accepts a tenant id from the client.
- **Authorization.** Every controller carries `[RequireModule(TRAVEL_AGENCY)]` and a `[RequirePermission]`; reports sit in
  three route groups so finance and margin reports keep their own permissions; the ERP checks permissions only.
- **Money.** `decimal(18,2)` with `CurrencyCode` everywhere; idempotency keys on receipts, supplier payments, credits,
  refunds and payouts; the payout cap is now enforced under a lock.
- **PII.** Passport and CNIC numbers are masked without `Travel.Traveler.ViewPII`, never logged (the pipeline behaviours do
  not serialise payloads), excluded from exports by default, and the reminder texts mask passport numbers.
- **Dates.** Every windowed report and list applies its range; an inverted range returns an empty set (asserted live).
- **Migrations.** The whole TravelNexus migration chain applies cleanly on an empty database built from
  `Database/CompleteSchema.sql` (the live fixture fails otherwise).

## 3. Not covered here

Penetration testing of the hosted environment, dependency CVE scanning and the nginx / Cloudflare configuration are
tracked in `docs/production-readiness/05-go-live-security-verification-plan.md`, not in this module review.
