# Platform Administration Foundation (Stage 1)

Living gap analysis for the 15-stage Platform Administration roadmap.
Stage 1 delivers the **shell + IA + nav + gates** only. Later stages upgrade existing hubs—they do not replace them.

## Role visibility

| Capability | Super Admin (`SUPER_ADMIN`) | Tenant Admin (`TENANT_ADMIN`) |
|------------|----------------------------|--------------------------------|
| Platform hub `/platform` | Yes (default home) | Yes if any platform permission |
| Tenants (cross-tenant) | Yes | No (`Platform.Tenants.*` excluded from template) |
| Organization (hierarchy / branches / departments) | Yes | Own tenant |
| Access Control hub + Users | Yes | Own tenant |
| Modules / Subscriptions | Yes | View/manage own tenant via existing hubs |
| Migrations | Yes (`Platform.Migrations.*`) | No |
| Database Reset / System Maintenance | Yes + Dev/Staging only (`Platform.System.Reset`) | No |
| Settings / Audit Logs | Yes | Own tenant (existing permissions) |

## Canonical route map (reuse, do not rebuild)

| Section | Canonical route | Existing surface |
|---------|-----------------|------------------|
| Hub | `/platform` | `platform-hub` (Stage 1) |
| Company | `/platform/tenants` | `tenant-list` / provision / detail |
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

## Stage matrix (1–15)

| # | Stage | Status | Notes |
|---|-------|--------|-------|
| 1 | Platform Administration Foundation | **Done (this stage)** | Hub, nav sync, ops permissions, child gates, gap doc |
| 2 | Company Management | Existing | Upgrade tenants/branches/depts from hub—do not rebuild |
| 3 | Subscription & License | Partial | UI + schema exist; hard license enforcement later |
| 4 | Module Management | Existing | Keep `module-management` |
| 5 | Feature Management | Missing | No feature-flag product yet |
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
| `Platform.Migrations.View` | View migration status |
| `Platform.Migrations.Manage` | Apply pending migrations |
| `Platform.System.Reset` | Database reset (still Super Admin + Dev/Staging) |

## Deferred (explicit non-goals of Stage 1)

- New Company / User / Role / Module / Subscription CRUD modules
- Feature flags, Menu Builder UI, Workspace/Dashboard builders, Data Scope engine
- Full Security Center / Audit Center productization
- Blanket `[RequirePermission]` across Bookings, Customers, Payments, Routes, Trips

## Handoff to Stage 2

Start from the hub **Company** section and harden the existing tenant/branch/department flows (validation, limits, branding), without introducing a parallel company module.
