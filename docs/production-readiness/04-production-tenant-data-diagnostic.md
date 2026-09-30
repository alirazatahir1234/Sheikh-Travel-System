# Production Tenant Data Diagnostic

| Field | Value |
|-------|--------|
| Phase | **Production Tenant Data Diagnostic** — READ ONLY |
| Date | 2026-09-29 |
| Production database confirmed | **YES** |
| Data modified | **NO** (SELECT only) |
| Schema modified | **NO** |
| Migrations run | **NO** |

---

## 1. Environment / database identity (no secrets)

| Field | Value |
|-------|--------|
| Config source | Railway linked project `sheikh-travel-backend-api`, environment **production**, service `Sheikh-Travel-System` |
| Public API URL | `https://sheikh-travel-system-production.up.railway.app` |
| Connection key | `ConnectionStrings__DefaultConnection` (Railway variable) |
| SQL host (ADO Server) | `20.174.1.230,1433` |
| `@@SERVERNAME` | `dc5b7dabd0d5` |
| `DB_NAME()` | `SheikhGo` |
| `SYSTEM_USER` | `sa` (login name only; password not recorded) |
| Edition | Developer Edition (64-bit) |
| Not production | Local Docker `127.0.0.1,1433` / `SheikhTravelSystemDB` / `940aff6640cf` (Phase 3A) — **not used** |

**Gate 0:** Remote Railway production host ≠ localhost / Docker / SQLEXPRESS → accepted as production for this diagnostic.

---

## 2. Tenant inventory

| Metric | Value |
|--------|--------|
| Total tenants | **2** |
| Active | 2 |
| Inactive | 0 |
| Duplicate slugs | none |

| Id | Name | Slug | IsActive | CreatedAt (UTC) |
|----|------|------|----------|-----------------|
| 1 | Sheikh Travel | `default` | true | 2026-06-05 |
| 2 | Vision Factory | `vision-factory` | true | 2026-06-25 |

**Observation:** Tenant 2 exists but has essentially no operational data (see §3) — only 1 user. Classification: **CLEAN** for inventory; multi-tenant readiness is thin for tenant 2.

---

## 3. Tenant-scoped table results

Tables checked from Phase 3C catalog (all present). No `Pricing` / `Invoices` base tables. Extra: `Trips`, `WebsiteContactRequests`, `WebsiteDemoRequests`, `VehicleTracking`.

| Table | Total rows | Distinct TenantIds | NULL TenantId | Invalid TenantId (not in Tenants) | Per TenantId | Class |
|-------|------------|--------------------|---------------|-------------------------------------|--------------|-------|
| Users | 10 | 2 | 0 | 0 | 1→9, 2→1 | CLEAN |
| Customers | 7 | 1 | 0 | 0 | 1→7 | CLEAN |
| Vehicles | 79 | 1 | 0 | 0 | 1→79 | CLEAN |
| Drivers | 19 | 1 | 0 | 0 | 1→19 | CLEAN |
| Routes | 8 | 1 | 0 | 0 | 1→8 | CLEAN |
| Bookings | 13 | 1 | 0 | 0 | 1→13 | CLEAN |
| Payments | 9 | 1 | 0 | 0 | 1→9 | CLEAN |
| FuelLogs | 11 | 1 | 0 | 0 | 1→11 | CLEAN |
| Maintenance | 4 | 1 | 0 | 0 | 1→4 | CLEAN |
| GpsDevices | 16 | 1 | 0 | 0 | 1→16 | CLEAN |
| Geofences | 4 | 1 | 0 | 0 | 1→4 | CLEAN |
| GpsAlertRules | 4 | 1 | 0 | 0 | 1→4 | CLEAN |
| GpsAlertEvents | 542 | 1 | 0 | 0 | 1→542 | CLEAN |
| GpsDeviceCommands | 5 | 1 | 0 | 0 | 1→5 | CLEAN |
| GpsPositions | 208498 | 1 | 0 | 0 | 1→208498 | CLEAN |
| GpsTrips | 2929 | 1 | 0 | 0 | 1→2929 | CLEAN |
| Notifications | 10685 | 1 | 0 | 0 | 1→10685 | CLEAN |
| AuditLogs | 1308 | 1 | 0 | 0 | 1→1308 | CLEAN |
| DriverAllowanceRules | 5 | 1 | 0 | 0 | 1→5 | CLEAN |
| PromoCodes | 1 | 1 | 0 | 0 | 1→1 | CLEAN |
| Trips | 4 | 1 | 0 | 0 | 1→4 | CLEAN |
| WebsiteContactRequests | 0 | 0 | 0 | 0 | — | CLEAN |
| WebsiteDemoRequests | 0 | 0 | 0 | 0 | — | CLEAN |

**No NULL TenantId rows. No TenantIds missing from `Tenants`.**

Tenant 2 operational counts: Users=1, Vehicles=0, Bookings=0, Customers=0.

---

## 4. Parent / child consistency

Initial joins against **active** (`IsDeleted=0`) parents flagged orphans; follow-up against all parents shows they are almost entirely **soft-deleted parents**, not missing FKs or cross-tenant IDs.

| Check | Non-deleted children | TenantMismatch | Soft-deleted parent | Truly missing parent | Class |
|-------|----------------------|----------------|---------------------|----------------------|-------|
| FuelLogs → Vehicles | 1 active fuel log | 0 | — | 0 (active path) | CLEAN |
| Maintenance → Vehicles | 4 | 0 | **4** | 0 | ORPHAN (soft-deleted vehicle) |
| Payments → Bookings | 9 | 0 | **7** | 0 | ORPHAN (soft-deleted booking) |
| Bookings → Customer/Vehicle/Driver | 3 active bookings | 0 / 0 / 0 | — | 0 | CLEAN |
| GpsDevices → Vehicles | 13 active devices | 0 | **10** (+1 NULL VehicleId) | 0 | ORPHAN / unassigned device |
| GpsPositions → Vehicles | 208498 | 0 | — | 0 | CLEAN |
| GpsTrips → Vehicles | 2929 | 0 | — | 0 | CLEAN |
| GpsAlertEvents → Vehicles | 542 | 0 | **4** | 0 | ORPHAN (soft-deleted vehicle) |

**No `DATA_MISMATCH` (child TenantId ≠ parent TenantId) on checked relationships.**

Recommended next action (human, later — not this phase): decide whether soft-deleted parent references should be retained for history, cascade soft-delete children, or cleaned. **Do not auto-repair.**

---

## 5. Routes

| Metric | Value |
|--------|--------|
| Total rows | 8 |
| TenantId distribution | all TenantId=1 |
| NULL TenantId | 0 |
| Invalid TenantId | 0 |
| Multi-tenant sharing | **No** — single tenant only |

Class: **CLEAN**. No ambiguous ownership adjudication required for Routes on this production DB.

---

## 6. DriverAllowanceRules

| Metric | Value |
|--------|--------|
| Total rows | 5 |
| TenantId distribution | all TenantId=1 |
| NULL TenantId | 0 |
| Invalid TenantId | 0 |
| Multi-tenant sharing | **No** |

Class: **CLEAN**. No ambiguous ownership adjudication required.

---

## 7. VehicleTracking

`VehicleTracking` has **no TenantId** column (expected). Isolation is via `VehicleId → Vehicles.TenantId`.

| Metric | Value |
|--------|--------|
| Total non-deleted rows | 208554 |
| Rows with existing vehicle | 208554 |
| Orphan tracking (no vehicle) | **0** |
| Distribution via Vehicles | TenantId 1 → 208554 |
| Unassociable with a tenant | **0** |

Class: **CLEAN**.

---

## 8. Platform / public exceptions

| Area | Observation | Class |
|------|-------------|-------|
| WebsiteContactRequests / WebsiteDemoRequests | 0 rows; TenantId column + DEFAULT 1 present | CLEAN (empty) |
| AuditLogs | 1308 rows, all TenantId=1; platform-admin read path remains intentional | CLEAN |
| PublicLead PublicByDesign | No production lead rows to evaluate | CLEAN |
| Tenant 2 | Exists with 1 user, no fleet/ops data | CLEAN / note for ops |

---

## 9. DEFAULT 1 inventory

**32** tables have a `TenantId` default of `((1))` (SQL Server default constraint). Including:

AuditLogs, Bookings, BookingSeatHolds, Customers, DriverAllowanceRules, Drivers, FuelLogs, Geofences, GpsAlertEvents, GpsAlertRules, GpsDeviceCommands, GpsDevices, GpsPositions, GpsTrips, Maintenance, Notifications, Payments, PromoCodes, Routes, Trips, Users, VehicleDocuments, Vehicles, WebsiteContactRequests, WebsiteDemoRequests, WebsiteFeatures, WebsiteLegalDocuments, WebsiteMedia, WebsitePages, WebsiteSections, WebsiteSettings, WhatsAppAccounts.

**No defaults were removed or altered.** Exposure: inserts that omit TenantId still land on tenant 1 at the DB layer — application Phase 3B/3C writes now set TenantId explicitly for hardened paths; default remains a residual risk until a future approved migration.

---

## 10. Critical findings

| Finding | Class | Severity |
|---------|-------|----------|
| Production identity confirmed via Railway (not local Docker) | CLEAN | — |
| Zero NULL / invalid TenantIds across catalog tables | CLEAN | — |
| Zero parent TenantId mismatches on checked FKs | CLEAN | — |
| Routes / DriverAllowanceRules single-tenant, unambiguous | CLEAN | — |
| VehicleTracking fully associable via Vehicles | CLEAN | — |
| Soft-deleted parent refs (Maintenance, Payments, GpsDevices, some alerts) | ORPHAN | Low–medium (data hygiene, not cross-tenant leak) |
| GpsDevices with NULL VehicleId (1) | ORPHAN | Low |
| Tenant 2 nearly empty | CLEAN | Informational |
| DEFAULT 1 still on 32 tables | REQUIRES_HUMAN_DECISION (future) | Residual risk — do not change now |

**No `DATA_MISMATCH` or `INVALID_TENANT` or `AMBIGUOUS_OWNERSHIP` on Routes/DriverAllowanceRules.**

---

## 11. Ambiguous records requiring human decision

| Item | Why human |
|------|-----------|
| Soft-deleted vehicle still referenced by active Maintenance / GpsDevices / alert events | Keep for audit history vs soft-delete/cascade children |
| Soft-deleted bookings still referenced by active Payments | Accounting history vs cleanup |
| Unassigned GpsDevice (NULL VehicleId) | Assign, retire, or leave as spare |
| When to remove `DEFAULT 1` | Only after app paths proven + optional backfill policy |

**Nothing classified as fixed** — diagnostic only.

---

## 12. Recommended next action (per finding)

| Finding | Recommended next action |
|---------|-------------------------|
| Overall tenant isolation data | Proceed to **security verification** gate (not Phase 4 yet) if product accepts orphan hygiene as non-blocking |
| Soft-delete orphans | Optional reconciliation ticket; **no auto UPDATE** |
| DEFAULT 1 | Keep; plan removal only after explicit approval post-verification |
| Tenant 2 empty | Product/ops decide whether Vision Factory should receive seed data or stay idle |
| Phase 4 | **Do not start** until this report is approved |

---

## Safety checklist

| Check | Status |
|-------|--------|
| Only SELECT | Yes |
| No UPDATE/DELETE/INSERT/MERGE | Yes |
| No ALTER/DROP/TRUNCATE | Yes |
| No migrations | Yes |
| No DEFAULT 1 removal | Yes |
| No TenantId value changes | Yes |
| No application code changes for this diagnostic | Yes (docs only) |
| Secrets not written into this document | Yes |

---

## STOP

Production diagnostic complete. Await human review before security verification / Phase 4 / any data repair.
