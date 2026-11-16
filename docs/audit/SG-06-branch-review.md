# Review: `feat/cross-tenant-admin-subscription-gate` vs SG-05, SG-06, SG-11, SG-43

Read-only review. I changed nothing. Two things you should know first:
- The working tree has an **unresolved merge conflict on `.gitignore`** (`UU`).
- `main...HEAD` is 145 files. Most of it is outside these four findings: four release `.aab`/`.apk` binaries (~350 MB), marketing assets, website pricing/i18n, a new `sheikhgo-mobile-overview` app, and the audit docs themselves. That breaks the "one branch per issue" rule. The relevant code is about 20 backend files, 6 ERP files and 6 Flutter files.

---

## 1. What is implemented and working

**Subscription expiry gate (backend).** This addresses the "Expired, its 5 users still have access" half of SG-43.
- `SubscriptionBillingLifecycle` gets PaidThrough → AccessExpires (+7-day grace), plus `ResolveSubscriptionStatus`, `ShouldDeactivateTenant` and `RemainingAccessDays`. These have unit tests (`SubscriptionAccessGateTests`, `SubscriptionBillingLifecycleTests`).
- `PlatformRepository.ProcessSubscriptionExpiryAndDeactivationAsync` sets the subscription to `Expired` and `Tenants.IsActive = 0`. Mark-paid reactivates the tenant.
- The migration `SubscriptionAccessExpiryMigration` is registered in `DatabaseMigrationRegistry`.
- Expired or deactivated tenants are blocked in three places, each returning the stable codes `tenant_deactivated` / `subscription_expired`:
  - `LoginCommandHandler`
  - `RefreshTokenCommandHandler`
  - the new `TenantSubscriptionAccessMiddleware`, registered after tenant resolution and before authorization
- License summary returns a `BillingMessage`.

**Subscription gate (ERP).**
- `auth.interceptor.ts` shows a toast on 402/403 and logs the user out on either code.
- `login.component.ts` shows the expiry message.
- `shell.component` adds a billing banner.

**Cross-tenant vehicle read.**
- `GET /api/admin/companies` and `GET /api/admin/vehicles?companyId=` (`AdminCrossTenantController`).
- `VehicleRepository.GetCrossTenantPagedAsync` is parameterised SQL and returns `CompanyId`/`CompanyName` on every row.
- Flutter consumes both endpoints (`admin_cross_tenant_api.dart`, `platform_company_filter.dart`, fleet hub). The company filter is persisted and sent as `X-Tenant-Id`, and `deleteAll()` on logout clears it.

**Tenant-user header protection is intact.** In `TenantResolutionPolicy`, a non-platform user whose `X-Tenant-Id` doesn't match their JWT tenant still gets a 403. A new test covers the super-admin no-header case.

## 2. Started but incomplete

| File | Missing |
| --- | --- |
| `Application/Common/PlatformPermissions.cs` | `Platform.Vehicles.ViewAll` is declared but **no migration seeds it** onto any role. SUPER_ADMIN still has 0 permissions. Only the role-name bypass grants it. |
| `Application/Features/Admin/AdminCrossTenantQueries.cs` | `GetAdminCompaniesQuery` writes **no audit event**. The vehicles audit does not record which tenants were read (see Q4). |
| `TenantSubscriptionAccessMiddleware.cs` | One uncached DB lookup per authenticated request. No test exercises the middleware itself; the test only asserts the constant strings. |
| `Frontend/sheikh-driver/...` | The app never handles `subscription_expired` / `tenant_deactivated` (grep finds nothing). The ERP handles them; the driver app will show a generic error. This breaks the "contract change updates both clients" rule. |
| `Frontend/sheikhgo-erp/...` | Doesn't call `/api/admin/*` at all. The new cross-tenant API exists only for Flutter. |
| `shared/components/tenant-picker/tenant-picker.component.ts:44` | Not touched, but see the regression below. |

**Regression against SG-11.** `tenant-picker` filters to `t.isActive`. This branch now sets `IsActive = 0` when a subscription expires. So expired tenants will disappear from the picker, just as suspended ones already do. Subscription Management uses that picker (`subscription-management.component.html:18`), so an expired tenant can't be selected there to renew. Before, only suspension was a one-way door; after this branch, expiry is one too.

## 3. Explicit permission or role-name check?

**It checks the role name. This branch adds more SG-05-style role-name checks; it does not remove any.**
- `PermissionAuthorizationHandler` (line ~18): any user with the `SUPER_ADMIN` role passes every check first. So `[RequirePermission(Platform.Vehicles.ViewAll)]` is decided by the role name for super admins. Revoking the permission would change nothing, and nobody holds it anyway.
- The new `EnsurePlatformCrossTenantReader` checks `currentUser.IsPlatformSuperAdmin || tenantContext.IsCrossTenantBypass`:
  - `IsPlatformSuperAdmin` is a role-claim check.
  - `IsCrossTenantBypass` is set only when `PlatformRoleClaims.IsPlatformOperator`, which is another role-name check (`TenantResolutionPolicy`).
- Further role-name checks added in this branch:
  - the `TenantSubscriptionAccessMiddleware` exemption (`IsPlatformOperator`)
  - `LoginCommandHandler` and `RefreshTokenCommandHandler`: `RoleCodes.Any(c == SUPER_ADMIN)`
  - Flutter `canCrossTenantVehicles`: `hasRole(superAdmin) || hasPermission(...)`
- `IsSuperAdminOnlyPermission` now covers `Platform.Vehicles.*`. That means only SUPER_ADMIN can ever pass it, so a future PLATFORM_AUDITOR role holding the permission would still be **denied**. This hard-codes the SG-05 problem further.

## 4. Does every cross-tenant read write an audit event naming the tenants read?

**No.**
- `/api/admin/companies` writes no audit event at all, and it lists every tenant.
- `/api/admin/vehicles` calls `auditService.LogAsync("ListAllCompanies" | "ListByCompany", "AdminVehicles", entityId: companyId)`. Problems with that call:
  - In "all companies" mode `entityId` is `null`. The event doesn't name the tenants whose rows came back.
  - `AuditEngine.RecordAsync` writes the event under the **caller's own tenant** (`TryTenantId()`), not the tenant that was read. So the read-from company's Audit Center would never show it.
  - If the super admin has no JWT tenant (the "full cross-tenant mode" branch), `AuditEngine` **skips the write silently** ("no tenant context", line ~33).
  - `AuditService.LogAsync` swallows exceptions, and the per-tenant audit policy can skip the write.
- Every legacy endpoint that a super admin reaches with `X-Tenant-Id: N` (acting inside another company) writes no "cross-tenant access" event either.

## 5. Is a client-supplied tenant id trusted without validation?

**Yes, for platform operators.** This behaviour existed before the branch, but the branch builds on it.
- `TenantResolutionPolicy.DecideAuthenticated`: when `isPlatformOperator` is true, `X-Tenant-Id` is accepted as-is. Nothing checks that the tenant exists, isn't deleted, or is in a switchable list. It is then passed to `tenantContext.SetTenant()`.
- The playbook requires "validated against that switchable list — never trusted blindly"; that isn't done yet.
- `?companyId=` on `/api/admin/vehicles` is also unvalidated. It only narrows the query, so the risk is low. But whatever arbitrary id the client sends ends up in the audit event.
- For tenant users the header is still validated against the JWT. That part is good.
- One more trap: the bypass flag is set on **every** super-admin request that has no header, not just `/api/admin/*`. Today only the admin queries read it. Any future handler that checks `IsCrossTenantBypass` to drop its tenant predicate becomes a cross-tenant leak (P0 class).

## 6. What is still missing to close SG-06 end to end

**Backend**
- `GET /api/platform/tenants/switchable` returning all tenants, **including Suspended/Expired/IsActive = 0**, with a status flag, gated by a real `Platform.Tenant.Read` permission.
- Validate `X-Tenant-Id` against that list in `TenantResolutionMiddleware`. A 403 alone isn't enough; it also needs an audit event.
- An explicit "all companies" contract for list endpoints that declare themselves platform-scope. It should be a declared flag on the endpoint, not an implicit no-header bypass. Every row carries `companyName`.
- Audit Center, Security Center and Workspace Management read endpoints must accept the switched tenant or all-companies mode. `AuditEngine.SearchAsync` currently throws without a `TenantId`.

**Angular (`Frontend/sheikhgo-erp`)**
- No header switcher exists. This branch only adds a billing banner to `shell.component`.
- There are two unsynchronised context services:
  - `PlatformTenantContextService` (used by `tenant-picker` and about 17 components)
  - `CompanyScopeService` (the only one `auth.interceptor.ts` reads for `X-Tenant-Id`)
- Selecting a tenant in the platform pages' picker does not set the header.
- Fixing this needs:
  - one `TenantContextService`
  - a header switcher next to the user menu showing the active company, with suspended/expired tenants marked and an "All companies" option
  - the interceptor sending `X-Tenant-Id` from that one service
  - removal of the page-local pickers (vehicles, drivers, GPS scope bar, Access Control, Module Registry, Subscription Management)
  - unblocking Security Center, Audit Center and Workspace Management

**Flutter.** Move its company filter onto the same `/switchable` endpoint and permission model.

---

## Remaining work, ordered by what blocks what

1. **Resolve the `.gitignore` conflict.** Split the unrelated work (binaries, marketing, website, mobile-overview) off this branch.
2. **SG-05 first.** Seed a `Platform.*` permission family (`Platform.Tenant.Read`, `Platform.Vehicles.ViewAll`, `Platform.Audit.ReadAll`, …) onto SUPER_ADMIN via a migration. Then:
   - remove the `SUPER_ADMIN` short-circuit in `PermissionAuthorizationHandler` and the `IsSuperAdminOnlyPermission` role lock
   - make `IsPlatformOperator`, `EnsurePlatformCrossTenantReader`, the login/refresh/middleware exemptions and Flutter's `canCrossTenantVehicles` check permissions instead
   - add a test that a SUPER_ADMIN with the permission revoked is denied

   Everything below depends on this.
3. **Cross-tenant audit.** Write the event under the **target** tenant(s), or as a platform-level event that lists the tenant ids read. Never skip it when the caller has no tenant. Do it for `/admin/companies` too.
4. **`/api/platform/tenants/switchable`** (needs 2). Include inactive/suspended/expired tenants. This fixes SG-11 and the expiry regression the branch introduces.
5. **Validate `X-Tenant-Id` against the switchable set** (needs 4). Replace the implicit no-header `CrossTenantBypass` with an opt-in that each platform-scope endpoint declares.
6. **Angular** (needs 4–5): unify the two context services, add the header switcher with "All companies", point the interceptor at it, and remove the page pickers. Then unblock Security, Audit and Workspace (the Audit read path needs all-companies support).
7. **Flutter**: handle `subscription_expired` / `tenant_deactivated`, and switch the company filter to `/switchable`.
8. **SG-43**: once expiry sets `IsActive = 0`, define and label the KPI ("Active, excluding suspended/expired") so the card and the table agree. Add a middleware-level test for the subscription gate. Consider caching the per-request gate lookup.

## How to verify each point by hand
- **Q3:** remove `Platform.Vehicles.ViewAll` from SUPER_ADMIN (it isn't there) and call `GET /api/admin/vehicles` as super admin. You still get a 200, which confirms the role-name bypass.
- **Q4:** call `/api/admin/vehicles` with no `companyId`, then run `SELECT TOP 5 * FROM AuditEvents ORDER BY Id DESC`. The `TenantId` is the caller's home tenant and `EntityId` is NULL. Call `/api/admin/companies` and no row appears.
- **Q5:** as super admin, send `X-Tenant-Id: 999999` (no such tenant) to any list endpoint. You get a 200 with empty data, not a 403.
- **SG-11 regression:** expire a test tenant's subscription and run the expiry job. Open Subscription Management, and the tenant is missing from Select Tenant.
