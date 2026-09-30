# Security Package 1 — Progress

## Phase 0 — Secrets

| Field | Status |
|-------|--------|
| Phase | **0 — Secrets** COMPLETE (awaiting approval for Phase 1) |
| Date | 2026-09-29 |

### Files changed

- [`Backend/SheikhTravelSystem.Application/Features/CustomerPortal/PortalAuthSettings.cs`](../../Backend/SheikhTravelSystem.Application/Features/CustomerPortal/PortalAuthSettings.cs) — DevMode default false
- [`Backend/SheikhTravelSystem.Application/Features/CustomerPortal/Commands/PortalAuthCommands.cs`](../../Backend/SheikhTravelSystem.Application/Features/CustomerPortal/Commands/PortalAuthCommands.cs) — OTP bypass only when `IsDevelopment()` + DevMode; OTP code not logged
- [`Backend/SheikhTravelSystem.Application/Common/Configuration/ProductionSecretsValidator.cs`](../../Backend/SheikhTravelSystem.Application/Common/Configuration/ProductionSecretsValidator.cs) — new fail-fast validator
- [`Backend/SheikhTravelSystem.API/Program.cs`](../../Backend/SheikhTravelSystem.API/Program.cs) — call validator at startup
- [`Backend/SheikhTravelSystem.Application/SheikhTravelSystem.Application.csproj`](../../Backend/SheikhTravelSystem.Application/SheikhTravelSystem.Application.csproj) / Tests csproj — Hosting + Configuration packages
- [`Backend/SheikhTravelSystem.Tests/CustomerPortal/PortalAuthTests.cs`](../../Backend/SheikhTravelSystem.Tests/CustomerPortal/PortalAuthTests.cs) — PortalAuth + validator tests
- [`.gitignore`](../../.gitignore), [`Backend/.gitignore`](../../Backend/.gitignore) — `**/appsettings.Development.json`, `**/appsettings.Local.json`
- [`Backend/docs/local-dev-config.example.json`](../../Backend/docs/local-dev-config.example.json) — local overlay template (not named `appsettings*`; ADR-009 safe)
- [`Backend/README.secrets.md`](../../Backend/README.secrets.md) — env / user-secrets documentation

### Changes made

1. Hardened PortalAuth so Production/Staging cannot use DevMode/DevOtpCode bypass (code-enforced; ignores config DevMode outside Development).
2. Non-Development hosts abort startup if required secrets are missing/placeholders (`ProductionSecretsValidator`).
3. Documented required env vars; fixed gitignore patterns so Development overlays stay local.
4. `appsettings.Development.json` was already untracked; local file left in place (not deleted).
5. **Tracked `appsettings.json` placeholder scrub deferred** — ADR-009 fails on removal hunks and on any change to that path; host env must override. Follow-up scrub requires an ADR-009 override merge after credential rotation.

### Tests executed

```bash
dotnet build Backend/SheikhTravelSystem.API/SheikhTravelSystem.API.csproj
dotnet test Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj --filter "FullyQualifiedName~PortalAuth|FullyQualifiedName~ProductionSecrets"
dotnet test Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj
```

Note: no `Backend/SheikhTravelSystem.sln` exists in the repo; API + test projects were used.

### Test results

| Suite | Result |
|-------|--------|
| PortalAuth + ProductionSecrets (7 tests) | **Passed** |
| Full `SheikhTravelSystem.Tests` | 696 passed, **9 failed** (pre-existing / unrelated to Phase 0 — e.g. GPS enterprise preference, `OptimizeTripRouteCommand` auditable coverage) |

### Security verification

| Check | Result |
|-------|--------|
| PortalAuth OTP bypass outside Development | Blocked in code |
| ProductionSecretsValidator on missing/placeholder keys | Fail-fast |
| `appsettings.Development.json` in git index | Not tracked |
| Package 1 diff includes `appsettings.json` | No (ADR-009) |
| Package 1 diff includes `appsettings*.json.example` | No (ADR-009) |

**Do not treat secrets as rotated.** Values previously committed remain in git history (and may still appear in tracked `appsettings.json` until the override scrub) until operators rotate them outside the repository.

### Manual actions still required

Operators must **rotate** and **reload** (Railway / App Service / user-secrets) — Phase 0 does not perform rotation:

1. SQL login password previously embedded in the connection string
2. `JwtSettings:Secret` (will invalidate existing JWTs)
3. Traccar password
4. Google Maps backend API key (Cloud Console regenerate + restrict)

Then set the env vars documented in [`Backend/README.secrets.md`](../../Backend/README.secrets.md). Host env overrides tracked `appsettings.json`.

Local developers: configure user-secrets or a private `appsettings.Development.json` (see [`Backend/docs/local-dev-config.example.json`](../../Backend/docs/local-dev-config.example.json)).

### Remaining risks

- Historical commits still contain prior secret material (history rewrite not in scope).
- Tracked `appsettings.json` scrub deferred to an ADR-009 override merge (path + removal-hunk rules).
- Production/Staging should bind secrets via host env; validator only fails closed when values are missing/placeholders.
- Full test suite still has 9 unrelated failures to triage separately.

---

## Phase 1 — Tenant Resolution

| Field | Status |
|-------|--------|
| Phase | **1 — Tenant Resolution** COMPLETE (awaiting approval for Phase 2) |
| Date | 2026-09-29 |

### Files changed

- [`Backend/SheikhTravelSystem.Application/Common/PlatformRoleClaims.cs`](../../Backend/SheikhTravelSystem.Application/Common/PlatformRoleClaims.cs) — shared IsInRole / `role` / `ClaimTypes.Role` checks
- [`Backend/SheikhTravelSystem.Application/Common/Multitenancy/TenantResolutionPolicy.cs`](../../Backend/SheikhTravelSystem.Application/Common/Multitenancy/TenantResolutionPolicy.cs) — pure Decide() policy
- [`Backend/SheikhTravelSystem.API/Middleware/TenantResolutionMiddleware.cs`](../../Backend/SheikhTravelSystem.API/Middleware/TenantResolutionMiddleware.cs) — JWT-authoritative resolution; 403 on mismatch; warnings
- [`Backend/SheikhTravelSystem.Infrastructure/Authentication/PermissionAuthorizationHandler.cs`](../../Backend/SheikhTravelSystem.Infrastructure/Authentication/PermissionAuthorizationHandler.cs) — uses `PlatformRoleClaims`
- [`Backend/docs/local-dev-config.example.json`](../../Backend/docs/local-dev-config.example.json) — documents `MultiTenancy:AllowAnonymousDefaultTenant` for local overlays (code default remains `true`)
- [`Backend/SheikhTravelSystem.Tests/Tenants/TenantResolutionPolicyTests.cs`](../../Backend/SheikhTravelSystem.Tests/Tenants/TenantResolutionPolicyTests.cs) — truth-table unit tests

### Architecture / security behavior

1. Authenticated JWT `tenant_id` is authoritative for normal users.
2. Normal user + `X-Tenant-Id` ≠ JWT → **HTTP 403**, middleware stops (`next` not called).
3. `SUPER_ADMIN` + foreign `X-Tenant-Id` → header tenant wins + Warning log.
4. Authenticated + foreign `X-Tenant-Slug` / `?tenant=` → JWT tenant kept + Warning (no reject).
5. Anonymous ignores `X-Tenant-Id`; may resolve via slug; unresolvable uses tenant `1` when `AllowAnonymousDefaultTenant=true` (logged).
6. Pipeline order unchanged: Authentication → TenantResolution → Authorization.

### Tests added / results

| Suite | Baseline (Phase 0) | After Phase 1 |
|-------|--------------------|---------------|
| `TenantResolutionPolicyTests` | n/a | **11 passed** |
| Full `SheikhTravelSystem.Tests` | 696 passed / 9 failed / 705 total | **707 passed / 9 failed / 716 total** |

Same 9 pre-existing failures (GPS enterprise / auditable coverage). No new failures from Phase 1.

```bash
dotnet build Backend/SheikhTravelSystem.API/SheikhTravelSystem.API.csproj
dotnet test --filter "FullyQualifiedName~TenantResolutionPolicyTests"
dotnet test Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj
```

### Manual verification

With API running and a Tenant-1 JWT (`Authorization: Bearer …`):

1. `X-Tenant-Id: 2` → expect **403**
2. `X-Tenant-Id: 1` → expect success as tenant 1
3. `X-Tenant-Slug: other-tenant` → expect tenant 1 data + Warning in logs
4. `?tenant=other-tenant` → expect tenant 1 data + Warning
5. SUPER_ADMIN JWT + `X-Tenant-Id: 2` → expect tenant 2 + Warning
6. Anonymous + valid `X-Tenant-Slug` → existing portal tenant resolution
7. Anonymous + `X-Tenant-Id` → header ignored (slug/default path only)

### Remaining risks

- Clients that previously switched tenants via `X-Tenant-Id` without SuperAdmin will now get 403 (intended).
- Anonymous default to tenant 1 remains until ops sets `MultiTenancy:AllowAnonymousDefaultTenant=false`.
- SuperAdmin override is logged at Warning only (no audit table write in this phase).
- Repository SQL tenant predicates are **not** yet enforced in this phase (Phase 3).

### STOP

**Phase 2 (Endpoint Protection) must not start until explicitly approved.**

---

## Phase 2 — Endpoint Protection

| Field | Status |
|-------|--------|
| Phase | **2 — Endpoint Protection** COMPLETE (awaiting approval for Phase 3) |
| Date | 2026-09-29 |

### Files changed

- [`Backend/SheikhTravelSystem.API/Controllers/DevController.cs`](../../Backend/SheikhTravelSystem.API/Controllers/DevController.cs) — class-level `[Authorize]` + `[RequirePermission(Platform.Security.Manage)]`; kept all `IsDevelopment()` guards
- [`Backend/SheikhTravelSystem.API/Controllers/LookupController.cs`](../../Backend/SheikhTravelSystem.API/Controllers/LookupController.cs) — PublicByDesign docs; `[EnableRateLimiting("public")]`; remains `[AllowAnonymous]`
- [`Backend/SheikhTravelSystem.Application/Features/Platform/PermissionCoverageClassifier.cs`](../../Backend/SheikhTravelSystem.Application/Features/Platform/PermissionCoverageClassifier.cs) — `PublicByDesignControllers` + `IsPublicByDesign()`
- [`Backend/SheikhTravelSystem.API/Controllers/PermissionCoverageController.cs`](../../Backend/SheikhTravelSystem.API/Controllers/PermissionCoverageController.cs) — Notes = `PublicByDesign` for allowlisted public controllers
- [`Backend/SheikhTravelSystem.Tests/Platform/PermissionCoverageClassifierTests.cs`](../../Backend/SheikhTravelSystem.Tests/Platform/PermissionCoverageClassifierTests.cs) — Dev Internal with SecurityManage; Lookup PublicByDesign
- [`Backend/SheikhTravelSystem.Tests/Security/DevControllerProtectionTests.cs`](../../Backend/SheikhTravelSystem.Tests/Security/DevControllerProtectionTests.cs) — attribute + env-guard + Lookup smoke tests
- [`Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj`](../../Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj) — ProjectReference to API (for controller unit tests)

### DevController protection

| Layer | Behavior |
|-------|----------|
| `[Authorize]` | Anonymous → 401 |
| `[RequirePermission(SecurityManage)]` | Authenticated without `Platform.Security.Manage` → 403 |
| `env.IsDevelopment()` | Non-Development → 404; seeder/devData never called |

Destructive actions covered: `seed`, `reseed`, `reset-admin`, `fix-driver-login`, `migrate-booking-number`. Class-level attributes apply to any future action added to this controller.

### LookupController PublicByDesign

- Confirmed: timezones (`TimeZoneInfo`), currencies/countries (`WorldData` in-memory) — **no tenant DB access**.
- Remains `[AllowAnonymous]`.
- Rate-limited with existing `"public"` policy (5 req/min fixed window in `Program.cs`).
- Permission coverage Notes: `PublicByDesign`.

### Permission coverage result

| Controller | Classification | Notes |
|------------|----------------|-------|
| DevController | **Internal** (unchanged; Internal checked before permissions) | Now also Authorize + SecurityManage at runtime |
| LookupController | **Public** | `PublicByDesign` |

Live `GET /api/platform/permission-coverage` not exercised in this phase (requires running API + SecurityManage token). Classification verified via unit tests matching the inventory classifier used by that endpoint.

### Tests added / results

| Suite | Baseline (Phase 1) | After Phase 2 |
|-------|--------------------|---------------|
| Focused Dev/Lookup/PermissionCoverage | n/a | **20 passed** |
| Full `SheikhTravelSystem.Tests` | 707 passed / 9 failed / 716 total | **717 passed / 9 failed / 726 total** |

Same 9 pre-existing failures (assignments, vehicle document validator, GPS enterprise preference, `OptimizeTripRouteCommand` auditable coverage). No new failures from Phase 2.

```bash
dotnet build Backend/SheikhTravelSystem.API/SheikhTravelSystem.API.csproj
dotnet test --filter "FullyQualifiedName~DevController|FullyQualifiedName~Lookup|FullyQualifiedName~PermissionCoverage"
dotnet test Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj
```

### Remaining findings (not fixed in Phase 2)

- [`AdminSystemController`](../../Backend/SheikhTravelSystem.API/Controllers/AdminSystemController.cs) — already `[Authorize]` + `SystemReset` + Dev/Staging gate — OK.
- [`TenantsController`](../../Backend/SheikhTravelSystem.API/Controllers/TenantsController.cs) `POST …/reset-admin-password` — intentional platform admin (`TenantsManage`); review separately if production hardening needs extra controls.
- `"public"` rate limit is 5/min — may be tight if signup UIs call all three lookup endpoints rapidly; tune later if needed.

### Remaining risks

- Repository SQL tenant predicates still not enforced (Phase 3).
- Live permission-coverage GET against a running host not recorded in this phase.
- Class-level auth on DevController does not remove the Development env requirement for successful execution.

### STOP

**Phase 3B (Repository Tenant Scoping) must not start until Phase 3A diagnostic is approved.**

---

## Phase 3A — Tenant Data Diagnostic

| Field | Status |
|-------|--------|
| Phase | **3A — Diagnostic ONLY** COMPLETE (awaiting approval / production re-run) |
| Date | 2026-09-29 |
| Report | [`02-phase3-data-diagnostic.md`](02-phase3-data-diagnostic.md) |

Read-only diagnostics against local Docker `SheikhTravelSystemDB` (`127.0.0.1` / `940aff6640cf`): **Scenario A** — single tenant (Id=1), zero parent mismatches on FuelLogs/Maintenance/Payments/Bookings; Routes and DriverAllowanceRules all TenantId=1. No data or application code modified. Re-run SQL on production before treating as production gate.

---

## Phase 3B — Batch 1

| Field | Status |
|-------|--------|
| Phase | **3B Batch 1** COMPLETE (awaiting approval for Batch 2) |
| Date | 2026-09-29 |

### Baseline (before Batch 1 code)

| Suite | Result |
|-------|--------|
| Full tests | **717 passed / 9 failed / 726 total** |
| Gap inventory | Dashboard fully unscoped; Booking Create/Update/Assign/SoftDelete unscoped; Payment writes/reads mostly unscoped; DriverApp `SyncLinkedBookingStatusAsync` unscoped + bypassed state machine |

### Repositories / files changed

- [`DashboardRepository.cs`](../../Backend/SheikhTravelSystem.Infrastructure/Persistence/Repositories/DashboardRepository.cs) + [`IDashboardRepository`](../../Backend/SheikhTravelSystem.Application/Common/Interfaces/Repositories/IDashboardRepository.cs) + [`GetDashboardSummaryQuery`](../../Backend/SheikhTravelSystem.Application/Features/Dashboard/Queries/GetDashboardSummaryQuery.cs)
- [`BookingRepository.cs`](../../Backend/SheikhTravelSystem.Infrastructure/Persistence/Repositories/BookingRepository.cs) — Create/Update/SoftDelete/Assign*
- [`PaymentRepository.cs`](../../Backend/SheikhTravelSystem.Infrastructure/Persistence/Repositories/PaymentRepository.cs) + interface + payment handlers/queries
- [`DriverAppRepository.cs`](../../Backend/SheikhTravelSystem.Infrastructure/Persistence/Repositories/DriverAppRepository.cs) + [`IDriverAppRepository`](../../Backend/SheikhTravelSystem.Application/Common/Interfaces/Repositories/IDriverAppRepository.cs) — removed unsafe sync; tenant params on bookings/payments/fuel/earnings
- [`DriverTripLifecycleCommands.cs`](../../Backend/SheikhTravelSystem.Application/Features/DriverApp/Commands/DriverTripLifecycleCommands.cs) — sync via `IBookingRepository.UpdateStatusAsync`
- Related DriverApp command/query callers
- [`TenantScopingBatch1Tests.cs`](../../Backend/SheikhTravelSystem.Tests/Security/TenantScopingBatch1Tests.cs)

### SQL protections added

| Area | Protection |
|------|------------|
| Dashboard aggregates | `AND TenantId = @TenantId`; cache `dashboard:summary:{tenantId}` |
| Booking Create | INSERT `TenantId`; Customer/Route lookups tenant-scoped |
| Booking Update/Delete/Assign | `WHERE … AND TenantId = @TenantId`; conflict checks scoped |
| Payment Create | INSERT `TenantId` from context |
| Payment reads/updates | `TenantId` on GetById/ByBooking/Report/Exists/UpdateStatus; booking lookup tenant-scoped |
| DriverApp | Bookings/Payments/Fuel/Trips/Attendance aggregates + ownership require TenantId |

### DriverApp booking-status sync — before / after

**Before:** `SyncLinkedBookingStatusAsync` ran:

```sql
UPDATE Bookings SET Status=… WHERE Id=@Id AND IsDeleted=0
-- no TenantId, no transition matrix, no driver/vehicle sync
```

**After:** `DriverAdvanceTripCommandHandler` calls `bookingRepository.UpdateStatusAsync(...)` which enforces `TenantId`, the booking transition matrix, and driver/vehicle status sync. Failures/not-found are logged; unscoped UPDATE path removed.

### Tests / build

| Suite | After Batch 1 |
|-------|---------------|
| `TenantScopingBatch1Tests` | **3 passed** |
| Full `SheikhTravelSystem.Tests` | **720 passed / 9 failed / 729 total** |
| Build API | **succeeded** |

Same 9 pre-existing failures. No new failures from Batch 1.

### Remaining risks

- **Production diagnostic still required** before merge/deploy (Phase 3A was local Docker only).
- Batch 2 not started: FuelLog, Maintenance, Route, DriverAllowance, Pricing.
- Batch 3 not started: Tracking, Customer, AuditLog, GPS, PublicLead (and any remaining repos).
- Phase 3C governance suite not started.
- Do not remove SQL `DEFAULT 1` on TenantId yet.

### STOP

**Do not start Batch 2 until Batch 1 is approved.**

---

## Phase 3B — Batch 2

| Field | Status |
|-------|--------|
| Phase | **3B Batch 2** COMPLETE (awaiting approval for Batch 3) |
| Date | 2026-09-29 |

### Baseline (before Batch 2 code)

| Suite | Result |
|-------|--------|
| Full tests | **720 passed / 9 failed / 729 total** |

| Repository | Methods | Gap before |
|------------|---------|------------|
| FuelLogRepository | GetPaged, GetById, Create, Update, Delete | 100% — no TenantId |
| MaintenanceRepository (legacy) | Create, Update, Delete, Exists, UpdateStatus, GetPaged, GetById | 100% — no TenantId; UpdateStatus missing IsDeleted |
| RouteRepository | Create, Update, Delete, Exists, GetById, GetPaged, GetListStats + RouteQueryFilters | 100% — no TenantId |
| DriverAllowanceRepository | GetPaged, GetById, Create, Exists, Update, SoftDelete, GetRouteContext, GetVehicleFuelType, GetActiveRules | 100% — no TenantId (incl. Routes/Vehicles helpers) |
| PricingRepository | GetRoutePricing, GetVehicleFuelAverage | 100% — no TenantId |

`MaintenanceModuleRepository` left unchanged (out of scope).

### Repositories / files changed

- [`FuelLogRepository.cs`](../../Backend/SheikhTravelSystem.Infrastructure/Persistence/Repositories/FuelLogRepository.cs) + [`IFuelLogRepository`](../../Backend/SheikhTravelSystem.Application/Common/Interfaces/Repositories/IFuelLogRepository.cs) + FuelLogs command/query handlers
- [`MaintenanceRepository.cs`](../../Backend/SheikhTravelSystem.Infrastructure/Persistence/Repositories/MaintenanceRepository.cs) + [`IMaintenanceRepository`](../../Backend/SheikhTravelSystem.Application/Common/Interfaces/Repositories/IMaintenanceRepository.cs) + legacy Maintenance handlers
- [`RouteRepository.cs`](../../Backend/SheikhTravelSystem.Infrastructure/Persistence/Repositories/RouteRepository.cs) + [`IRouteRepository`](../../Backend/SheikhTravelSystem.Application/Common/Interfaces/Repositories/IRouteRepository.cs) — `RouteQueryFilters.Build(tenantId, …)` + Routes handlers
- [`DriverAllowanceRepository.cs`](../../Backend/SheikhTravelSystem.Infrastructure/Persistence/Repositories/DriverAllowanceRepository.cs) + [`IDriverAllowanceRepository`](../../Backend/SheikhTravelSystem.Application/Common/Interfaces/Repositories/IDriverAllowanceRepository.cs) + DriverAllowance CRUD + `CalculateDriverAllowanceQuery`
- [`PricingRepository.cs`](../../Backend/SheikhTravelSystem.Infrastructure/Persistence/Repositories/PricingRepository.cs) + [`IPricingRepository`](../../Backend/SheikhTravelSystem.Application/Common/Interfaces/Repositories/IPricingRepository.cs) + [`CalculatePriceCommand`](../../Backend/SheikhTravelSystem.Application/Features/Pricing/Commands/CalculatePriceCommand.cs)
- [`TenantScopingBatch2Tests.cs`](../../Backend/SheikhTravelSystem.Tests/Security/TenantScopingBatch2Tests.cs)

### SQL protections added

| Area | Protection |
|------|------------|
| FuelLogs CRUD | INSERT `TenantId`; SELECT/UPDATE/soft-delete `AND TenantId = @TenantId` |
| Maintenance (legacy) | INSERT `TenantId`; mutations + GetPaged/GetById/Exists/UpdateStatus scoped; UpdateStatus also `IsDeleted = 0` |
| Routes | INSERT `TenantId`; mutations scoped; list/stats via `RouteQueryFilters` `WHERE … AND TenantId = @TenantId` |
| DriverAllowanceRules | Full CRUD + active rules scoped; Routes/Vehicles helper lookups scoped so calculate cannot cross tenants |
| Pricing | Route + Vehicle fuel-average lookups require `TenantId` (+ `IsDeleted` on Vehicles) |

### Manual SQL review

All public CRUD/read paths in the five Batch 2 repos now include TenantId. No intentionally unscoped methods remain in those files. `MaintenanceModuleRepository` not reviewed (out of scope).

### Tests / build

| Suite | After Batch 2 |
|-------|---------------|
| `TenantScopingBatch2Tests` | **6 passed** |
| Full `SheikhTravelSystem.Tests` | **726 passed / 9 failed / 735 total** |
| Build API | **succeeded** |

Same 9 pre-existing failures. No new failures from Batch 2. Pricing formula / Maintenance module tests unchanged in intent.

### Remaining risks

- **Production diagnostic still required** before merge/deploy (Phase 3A was local Docker only).
- Batch 3 not started.
- Phase 3C governance suite not started.
- Do not remove SQL `DEFAULT 1` on TenantId yet.
- Routes / DriverAllowanceRules use existing TenantId column only — no ownership model invent or data migration.

### STOP

**Do not start Batch 3 or Phase 3C until Batch 2 is approved.**

---

## Phase 3B — Batch 3

| Field | Status |
|-------|--------|
| Phase | **3B Batch 3** COMPLETE (awaiting approval for Phase 3C) |
| Date | 2026-09-29 |

### Baseline (before Batch 3)

| Suite | Result |
|-------|--------|
| Full tests | **726 passed / 9 failed / 735 total** |

### Repositories inspected

| Repository | Decision |
|------------|----------|
| TrackingRepository (`VehicleTracking`) | **Changed** — no TenantId column; scope via Vehicles join |
| CustomerRepository | **Changed** — complete CRUD/GetById/CNIC (lists already scoped) |
| AuditLogRepository | **Verify only** — already `a.TenantId = @TenantId` + `IPlatformScope` |
| GpsDeviceRepository | **Changed** — list/CRUD/commands/supported/send scoped |
| GpsTrackingRepository | **Changed** (selective) — geofences, alert rules, position history |
| GpsPositionIngestionHelper | **Changed** — GpsPositions INSERT writes TenantId from Vehicles |
| PublicLeadRepository | **Preserved** — `TenantId = 1` PublicByDesign marketing inbox |

### Tenant-boundary decisions

| Area | Approach |
|------|----------|
| **VehicleTracking** | No column/migration. Insert only if vehicle owned by tenant; Live/History `INNER JOIN Vehicles … TenantId`. |
| **Customer** | `GetRequiredTenantId()` inside repo; INSERT TenantId; mutations/GetById/CNIC per-tenant. |
| **AuditLog** | Keep platform-admin-aware tenant filter via `IPlatformScope` (no blind change). |
| **PublicLead** | Intentional platform marketing capture to tenant 1; anonymous forms do not use `GetRequiredTenantId()`. WhatsApp leads already tenant-aware — untouched. |
| **GPS Geofences / AlertRules / Devices** | Use `TenantId` from existing `TenantSchemaMigration` (no new migration). Devices also use `TrackerTenantSql.DeviceScopeFilter` for null-TenantId legacy rows. |
| **GPS position history** | Vehicle ownership check + Vehicles join before reading positions / Traccar link. |

### Files changed (high level)

- Tracking: interface, repository, UpdateLocation / GetLive / GetHistory handlers
- CustomerRepository CRUD + GetById
- GpsDeviceRepository (devices + JWT-facing commands)
- GpsTrackingRepository (geofence CRUD/list/assignments/stats, alert rules, position history)
- GpsPositionIngestionHelper (GpsPositions TenantId)
- [`TenantScopingBatch3Tests.cs`](../../Backend/SheikhTravelSystem.Tests/Security/TenantScopingBatch3Tests.cs)

### Intentionally unscoped / deferred

- Device UniqueId poll paths (`GetPendingDeviceCommands`, `CompleteDeviceCommand`) — device-facing, not JWT tenant.
- Remaining GPS analytics SQL not in the Batch 3 method list (deferred; not Phase 3C).
- PublicLead hardcoded tenant 1 — PublicByDesign.

### Tests / build

| Suite | After Batch 3 |
|-------|---------------|
| `TenantScopingBatch3Tests` | **5 passed** |
| Full `SheikhTravelSystem.Tests` | **731 passed / 9 failed / 740 total** |
| Build API | **succeeded** |

Same 9 pre-existing failures. No new failures from Batch 3.

### Remaining risks

- **Production diagnostic still required** before merge/deploy (Phase 3A was local Docker only).
- Some GPS analytics paths still lack TenantId — deferred beyond Batch 3 method list.
- Phase 3C governance suite not started.
- Do not remove SQL `DEFAULT 1` on TenantId yet.
- No migrations / data repair in this batch.

### STOP

**Do not start Phase 3C until Batch 3 is approved.**

---

## Phase 3C — Tenant Governance

| Field | Status |
|-------|--------|
| Phase | **3C — Tenant Governance** COMPLETE (awaiting approval before production diagnostic) |
| Date | 2026-09-29 |

### Baseline (before Phase 3C)

| Suite | Result |
|-------|--------|
| Full tests | **731 passed / 9 failed / 740 total** |

### What was added

Automated source-level safeguards under [`Backend/SheikhTravelSystem.Tests/Security/Governance/`](../../Backend/SheikhTravelSystem.Tests/Security/Governance/):

| File | Role |
|------|------|
| [`TenantScopedTableCatalog.cs`](../../Backend/SheikhTravelSystem.Tests/Security/Governance/TenantScopedTableCatalog.cs) | Inventory of TenantId tables (aligned with `TenantSchemaMigration`) + parent-scoped `VehicleTracking` + strict Phase 3B repository list |
| [`TenantGovernanceAllowlist.cs`](../../Backend/SheikhTravelSystem.Tests/Security/Governance/TenantGovernanceAllowlist.cs) | Narrow documented exceptions (PublicByDesign, platform repos, deferred GPS writers, device UniqueId paths, seeders/migrations) |
| [`TenantSqlGovernanceScanner.cs`](../../Backend/SheikhTravelSystem.Tests/Security/Governance/TenantSqlGovernanceScanner.cs) | Scans Infrastructure repositories/services for unsafe INSERT / VehicleTracking parent joins / Phase 3B SELECT·UPDATE·DELETE gaps; checks strict repos for tenant context |
| [`TenantGovernanceTests.cs`](../../Backend/SheikhTravelSystem.Tests/Security/Governance/TenantGovernanceTests.cs) | 8 governance tests |

### Governance coverage

1. Tenant-scoped table inventory matches migration list  
2. Strict repositories must use `ITenantContext` / `GetRequiredTenantId` / `tenantId` / `TenantId` SQL / `TrackerTenantSql`  
3. Runtime `INSERT INTO` tenant-owned tables must include `TenantId` (allowlist for deferred writers)  
4. `VehicleTracking` access must use Vehicles tenant boundary  
5. Phase 3B core repos must not regress to unscoped SELECT/UPDATE/DELETE  
6. PublicLead remains PublicByDesign (`TenantId = 1`)  
7. AuditLog keeps `a.TenantId = @TenantId`  
8. Allowlist entries require non-empty reasons  

### Intentionally allowlisted (not fixed in 3C)

- PublicLead marketing inserts → tenant 1  
- Platform / Auth / Tenant / Website repositories (cross-tenant by design)  
- GpsAlertWriter / GpsTripPersistence / PaymentGateway recorder / Traccar device import (deferred)  
- Device UniqueId command poll paths  
- DatabaseSeeder + Migrations (not runtime API)

### Tests / build

| Suite | After Phase 3C |
|-------|----------------|
| `TenantGovernance` | **8 passed** |
| Full `SheikhTravelSystem.Tests` | **739 passed / 9 failed / 748 total** |
| Build | **succeeded** (via test build) |

Same 9 pre-existing failures. No new failures from Phase 3C.

### Remaining risks

- **Production database diagnostic still required** before merge/deploy.  
- Deferred GPS analytics / report SQL not fully gated by the Phase 3B-core SELECT scanner (by design — avoid false-positive flood). Expand later if needed.  
- Do not remove SQL `DEFAULT 1` on TenantId yet.  
- Phase 4 workflow gaps not started.

### STOP

**Do not start production diagnostic changes, Phase 4, migrations, or DEFAULT 1 removal until Phase 3C is approved.**

---

## Production Tenant Data Diagnostic

| Field | Status |
|-------|--------|
| Phase | **Production Tenant Data Diagnostic** COMPLETE (READ ONLY) |
| Date | 2026-09-29 |
| Report | [`04-production-tenant-data-diagnostic.md`](04-production-tenant-data-diagnostic.md) |
| Production confirmed | **YES** — Railway production `SheikhGo` @ `20.174.1.230` (`@@SERVERNAME` `dc5b7dabd0d5`) |
| Data modified | **NO** |

Two active tenants (Sheikh Travel + Vision Factory). Catalog tables: no NULL/invalid TenantIds; no parent TenantId mismatches. Soft-deleted parent orphans noted (hygiene). Routes / DriverAllowanceRules / VehicleTracking: CLEAN. DEFAULT 1 still on 32 tables (inventory only). Phase 4 not started.
