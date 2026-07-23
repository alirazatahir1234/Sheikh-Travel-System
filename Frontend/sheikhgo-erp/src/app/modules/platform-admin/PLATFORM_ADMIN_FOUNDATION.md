# Platform Administration Foundation

Living gap analysis for the 15-stage Platform Administration roadmap.
Stage 1 delivered the **shell + IA + nav + gates**. Stage 2 reframes tenants as the **Company** business model (product language) with a thin Feature Registry—without renaming `Tenants` / `TenantId`.

## Role visibility

| Capability | Super Admin (`SUPER_ADMIN`) | Tenant Admin (`TENANT_ADMIN`) |
|------------|----------------------------|--------------------------------|
| Platform hub `/platform` | Yes (default home) | Yes if any platform permission |
| Companies (cross-tenant; permission `Platform.Tenants.*`) | Yes | No (`Platform.Tenants.*` excluded from template) |
| Organization (hierarchy / branches / departments) | Yes | Own tenant |
| Access Control hub + Users | Yes | Own tenant |
| Modules / Subscriptions | Yes | View/manage own tenant via existing hubs |
| Migrations | Yes (`Platform.Migrations.*`) | No |
| Database Reset / System Maintenance | Yes + Dev/Staging only (`Platform.System.Reset`) | No |
| Settings / Audit Logs | Yes | Own tenant (existing permissions) |

## Canonical route map (reuse, do not rebuild)

| Section | Canonical route | Existing surface |
|---------|-----------------|------------------|
| Hub | `/platform` | `platform-hub` |
| Company | `/platform/tenants` (alias `/platform/companies`) | `tenant-list` / provision / detail — UI says **Companies** |
| Organization | `/platform/organization-designer` | hierarchy feature |
| Branches / Departments | `/platform/branches`, `/platform/departments` | existing CRUD lists |
| Identity | `/platform/access-control` | Users / Roles / Permissions / Policies / Templates tabs |
| Users (standalone) | `/users` | users module (deep link OK) |
| Modules | `/platform/module-management` | existing |
| Subscriptions | `/platform/subscription-management` | existing |
| Migrations | `/platform/migrations` | existing |
| System Maintenance | `/platform/maintenance` | database reset (label clarified) |
| Settings | `/settings` | settings module |
| Audit | `/audit-logs` | audit-logs module |

### Stage 1 redirects / duplicates

| Legacy entry | Action |
|--------------|--------|
| `/platform` → `branches` | Replaced by hub |
| `/platform/roles` | Redirect → `/platform/access-control?tab=roles` |
| Local `organization-designer` component (unwired) | Leave unused; hierarchy feature is canonical |
| Nav label “Maintenance” under Platform | Renamed to **Database Reset** / System Maintenance |

### Stage 2 aliases

| Entry | Action |
|-------|--------|
| `/platform/companies` | Redirect → `/platform/tenants` |
| Company UI copy | Tenants → Companies (routes + `Platform.Tenants.*` codes unchanged) |
| `GET /api/platform/company/context` | Read-only company context (branding, branch/dept, modules, features) |
| Feature Registry | `FeatureDefinitions` + `TenantFeatures` metadata; list APIs only |

## Stage matrix (1–15)

| # | Stage | Status | Notes |
|---|-------|--------|-------|
| 1 | Platform Administration Foundation | **Done** | Hub, nav sync, ops permissions, child gates, gap doc |
| 2 | Company Business Model | **Done (this stage)** | Company vocabulary; DTO aliases; hierarchy/capabilities strip; thin Feature Registry; mobile read-only context |
| 3 | Subscription & License | Partial | Continues on Company (tenant) record; hard license enforcement later |
| 4 | Module Management | Existing | Keep `module-management` |
| 5 | Feature Management | Missing | Upgrades Feature Registry into Feature Management / flags |
| 6 | User Management | Existing | `/users` + Access Control Users tab |
| 7 | Role Management | Existing | Access Control Roles tab (canonical) |
| 8 | Permission Management | Existing | Access Control Permissions tab |
| 9 | Menu Builder | Partial | Schema + `menus/me`; no CRUD builder UI |
| 10 | Workspace Builder | Missing | |
| 11 | Dashboard Builder | Missing | Fixed dashboards only |
| 12 | Data Scope Engine | Missing | Tenant isolation only today |
| 13 | Security Center | Partial | Access Policies tab + settings flags |
| 14 | Audit Center | Partial | View/filter audit logs |
| 15 | Backend Permission Enforcement | Partial | Strong on platform/fleet; gaps on bookings/payments/etc. |

## Foundation permissions (Stage 1)

| Code | Purpose |
|------|---------|
| `Platform.Migrations.View` | View schema migration status |
| `Platform.Migrations.Manage` | Apply pending migrations |
| `Platform.System.Reset` | Database reset (still Super Admin + Dev/Staging) |

## Stage 2 deliverables

- **Persistence:** `Tenants` / `TenantId` unchanged; Company is product/API alias language (`companyId` / `companyName`).
- **Feature Registry:** Seeded catalog + per-company enablement rows; read-only list endpoints (`/api/platform/features/*`). No Feature Builder / runtime flags.
- **ERP:** Companies copy in hub/list/menus; company detail Hierarchy/Capabilities strip + feature metadata list.
- **Mobile:** Read-only company context after login (profile / more / dashboard header). No company admin CRUD on Flutter.

## Deferred (explicit non-goals of Stages 1–2)

- Renaming `Tenants` table or `TenantId` columns
- Feature Management / Feature Builder / runtime feature flags (Stage 5)
- Mobile Company / Branch / Department / Module admin CRUD
- Data Scope Engine (Stage 12)
- Rebuilding tenant/branch/department modules from scratch
- Blanket `[RequirePermission]` across Bookings, Customers, Payments, Routes, Trips

## Handoff to Stage 3

Continue Subscription & License on the Company (tenant) record. Stage 5 upgrades the Feature Registry into Feature Management.
