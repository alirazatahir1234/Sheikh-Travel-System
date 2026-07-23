# Platform Administration Foundation

Living gap analysis for the 15-stage Platform Administration roadmap.

- Stage 1: shell + IA + nav + gates
- Stage 2: Company business model + thin Feature Registry
- Stage 3: Module Registry metadata on existing `Modules` / Module Management (this stage)

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
| Modules | `/platform/module-management` | Module Registry UI (reuse; no new CRUD) |
| Subscriptions | `/platform/subscription-management` | existing (Stage 4 will harden) |
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

### Stage 3 Module Registry

| Entry | Action |
|-------|--------|
| `Modules` table | Metadata columns (category, version, status, capability flags, deps, …) |
| Catalog seed | Active enableable codes + Coming Soon / Beta product modules |
| `GET /api/platform/modules` | Enableable definitions (enriched) |
| `GET /api/platform/modules/catalog` | Full registry catalog |
| `GET /api/platform/modules/company` | Company installed + licensed (= installed) |
| `GET /api/platform/modules/{codeOrId}` | Single registry entry |
| Module Management UI | Metadata display + filters; toggles only for Active enableable |
| Mobile | Company context `modules[]` parsed; read-only chips on profile / more |

## Stage matrix (1–15)

| # | Stage | Status | Notes |
|---|-------|--------|-------|
| 1 | Platform Administration Foundation | **Done** | Hub, nav sync, ops permissions, child gates, gap doc |
| 2 | Company Business Model | **Done** | Company vocabulary; Feature Registry; mobile context |
| 3 | Module Registry | **Done (this stage)** | Metadata on `Modules`; catalog/company APIs; Module Management enrichment; mobile module list |
| 4 | Subscription & License | Partial | Will consume Module Registry for company access; hard enforcement later |
| 5 | Feature Management | Missing | Upgrades Feature Registry into Feature Management / flags |
| 6 | User Management | Existing | `/users` + Access Control Users tab |
| 7 | Role Management | Existing | Access Control Roles tab (canonical) |
| 8 | Permission Management | Existing | Access Control Permissions tab; future Permission Engine |
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

## Stage 3 deliverables

- **Module Registry:** Metadata on existing `Modules` (no duplicate tables). Status: Active / Beta / Coming Soon / Deprecated / Disabled.
- **APIs:** Catalog, company, by-key read models; existing enable/disable PUT unchanged.
- **ERP:** Module Management shows category, version, dependencies, status, Mobile/AI/GPS, Installed/Licensed.
- **Mobile:** Parses installed `modules[]`; display-only chips. No module admin.
- **Still missing (later):** Runtime subscription enforcement (Stage 4), feature flags (Stage 5), permission/menu/workspace/dashboard builders.

## Deferred (explicit non-goals of Stages 1–3)

- Renaming `Tenants` table or `TenantId` columns
- Duplicate Module CRUD / installer / builder
- Subscription & License enforcement (Stage 4)
- Feature Management / Feature Builder / runtime feature flags (Stage 5)
- Permission Engine, Menu/Workspace/Dashboard builders
- Mobile Company / Branch / Department / Module admin CRUD
- Data Scope Engine (Stage 12)

## Handoff to Stage 4

Use Module Registry metadata so **Subscription & License** can determine which modules each company may access. Stage 5 upgrades the Feature Registry into Feature Management.
