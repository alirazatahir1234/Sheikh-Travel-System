# SheikhGo ERP — Screen Permission Matrix

**Purpose:** Define which **permission** a user needs to **see and open** each ERP screen. Use this as the source of truth for role templates, menu seeding, Access Control, and QA.

**How access works (stacked filters):**

1. **Authenticated** — JWT / login required (except `/auth/login`).
2. **Module enabled** — Company Module Registry must enable the related enterprise module (e.g. `FLEET`, `TRAVEL`).
3. **Workspace soft focus** — Active workspace ModuleKeys can hide whole sidebar groups (e.g. Super Admin → `platform` hides Fleet).
4. **Menu permission** — Sidebar item shown only if user has the menu’s `PermissionCode` (OR Super Admin bypass on API).
5. **Route guard** — Angular `permissionGuard` requires **any one** of `data.permissions` (OR logic).

A user **sees a screen** only when **all** applicable layers allow it.

---

## Quick reference — Screen → View permission

| Screen / area | Route | Permission to **open** (route guard) | Typical sidebar group |
|---------------|-------|--------------------------------------|------------------------|
| Login | `/auth/login` | *(public)* | — |
| Profile | `/profile` | *(auth only)* | — |
| Driver My Trips | `/my-trips` | *(Driver role)* | Driver layout |
| Driver Fuel | `/my-trips/fuel` | *(Driver role)* | Driver layout |
| Dashboard | `/dashboard` | `Platform.Dashboard.View` | Dashboard |
| Bookings | `/bookings` | `Booking.View` | Operations |
| Trips | `/trips` | `Trip.View` | Operations |
| Routes | `/routes` | `Route.View` | Operations |
| Dispatch (live) | `/trips/live` | `Trip.View` | Operations |
| Fleet home | `/fleet/**` | `Vehicle.View` | Fleet |
| Vehicles | `/vehicles` | `Vehicle.View` | Fleet |
| Drivers | `/drivers` | `Driver.View` | Fleet |
| Assignments | `/fleet/assignments` | `Vehicle.View` | Fleet |
| Inspections | `/fleet/inspections` | `Vehicle.View` | Fleet |
| Compliance | `/fleet/compliance` | `Vehicle.View` | Fleet |
| Maintenance (fleet) | `/fleet/maintenance/**` | `Vehicle.View` *(parent)* + APIs use `Maintenance.*` | Fleet Ops |
| Service records | `/maintenance/service-records` | `Maintenance.View` | Fleet Ops |
| Fuel logs | `/fuel-logs` | `Fuel.View` | Fleet Ops / Finance |
| GPS Tracking | `/gps-tracking/**` | `GPS.View` | Fleet Ops |
| Customers | `/customers` | `Customer.View` | Customers |
| Payments | `/payments` | `Payment.View` | Finance |
| Reports | `/reports` | `Report.View` | Analytics |
| Notifications | `/notifications` | `Notification.View` | Analytics / Admin |
| AI Center | `/ai` | `Ai.View` | Analytics / Admin |
| Users | `/users` | `Platform.Users.View` | Identity |
| Driver allowance | `/driver-allowance-rules` | `Driver.Manage` | Access Control |
| Settings | `/settings` | `Platform.Settings.View` | Platform / Admin |
| Platform hub | `/platform` | Any of several Platform.* (see below) | Platform |
| Companies | `/platform/tenants` | `Platform.Tenants.View` | Organization |
| Company provision | `/platform/tenants/new` | `Platform.Tenants.Manage` | Organization |
| Branches | `/platform/branches` | `Platform.Branches.Manage` | Organization |
| Departments | `/platform/departments` | `Platform.Departments.Manage` | Organization |
| Org designer | `/platform/organization-designer` | `Platform.Branches.Manage` **OR** `Platform.Departments.Manage` **OR** `Platform.Tenants.View` | Organization |
| Access Control | `/platform/access-control` | `Platform.Roles.View` **OR** `Platform.Users.View` | Identity |
| Modules | `/platform/module-management` | `Platform.Tenants.View` **OR** `Platform.Tenants.Manage` | Platform |
| Features | `/platform/feature-management` | `Platform.Tenants.View` **OR** `Platform.Tenants.Manage` | Platform |
| Menus | `/platform/menu-management` | `Platform.Menus.Manage` | Platform |
| Workspaces | `/platform/workspace-management` | `Platform.Workspaces.Manage` | Platform |
| Dashboards admin | `/platform/dashboard-management` | `Platform.Dashboards.View` **OR** `Platform.Dashboards.Manage` | Platform |
| Security Center | `/platform/security-center` | `Platform.Security.View` **OR** `Platform.Security.Manage` | Platform |
| Permission Coverage | `/platform/permission-coverage` | `Platform.Security.Manage` | Platform |
| Audit Center | `/platform/audit-center` | `Platform.Audit.View` **OR** `Platform.AuditLogs.View` **OR** `Platform.Audit.Manage` | Platform |
| GPS Control Center | `/platform/gps-control-center` | `Platform.Gps.Control.View` (+ manage/execute/simulator for write actions) | Platform |
| Subscriptions | `/platform/subscription-management` | `Platform.Tenants.View` **OR** `Platform.Tenants.Manage` | Platform |
| Migrations | `/platform/migrations` | `Platform.Migrations.View` **OR** `Platform.Migrations.Manage` | Platform |
| Database Reset | `/platform/maintenance` | `Platform.System.Reset` | Platform |

---

## 1. Business screens (detail)

### 1.1 Operations (Travel)

| Screen | Route | Open (View) | Create / write (API) | Module to enable |
|--------|-------|-------------|----------------------|------------------|
| Bookings list / wizard / detail | `/bookings`, `/bookings/new`, `/bookings/:id` | `Booking.View` | `Booking.Create`, `Booking.Update`, `Booking.Delete` | `TRAVEL` |
| Trips dashboard / list / calendar / live / reports | `/trips/**` | `Trip.View` | `Trip.Create`, `Trip.Update`, `Trip.Delete`, `Trip.Assign` | `TRAVEL` |
| Routes | `/routes` | `Route.View` | `Route.Create`, `Route.Update`, `Route.Delete` | `TRAVEL` |

**Sidebar group:** Operations — needs enabled legacy keys `bookings` **or** `routes` (from `TRAVEL`).

### 1.2 Fleet

| Screen | Route | Open (View) | Create / write (API) | Module |
|--------|-------|-------------|----------------------|--------|
| Fleet dashboard | `/fleet/dashboard` | `Vehicle.View` | — | `FLEET` |
| Vehicles | `/vehicles` | `Vehicle.View` | `Vehicle.Create`, `Vehicle.Update`, `Vehicle.Delete` | `FLEET` |
| Drivers | `/drivers` | `Driver.View` | `Driver.Create`, `Driver.Update`, `Driver.Delete`, `Driver.Assign`, `Driver.Manage`, `Driver.ManageStatus` | `FLEET` |
| Assignments | `/fleet/assignments` | `Vehicle.View` | `Driver.Assign` (APIs) | `FLEET` |
| Inspections / Compliance | `/fleet/inspections`, `/fleet/compliance` | `Vehicle.View` | (feature APIs) | `FLEET` |
| Maintenance UI under fleet | `/fleet/maintenance/**` | Parent: `Vehicle.View` | `Maintenance.View`, `Maintenance.Manage`, `Maintenance.Request.*`, `Maintenance.WorkOrder.Manage`, etc. | `FLEET` |
| Service records | `/maintenance/service-records` | `Maintenance.View` | `Maintenance.Manage` | `FLEET` |
| Fuel logs | `/fuel-logs` | `Fuel.View` | `Fuel.Create`, `Fuel.Update` | `FLEET` |

**Sidebar group:** Fleet — needs `vehicles` / `drivers` / `fuel-logs` / `maintenance` / `gps-tracking` (from `FLEET` + `GPS`).

### 1.3 GPS

| Screen | Route | Open | Finer API permissions |
|--------|-------|------|------------------------|
| Live / history / trips / geofences / devices / analytics | `/gps-tracking/**` | `GPS.View` | `Gps.Alert*`, `Gps.Command*` |
| Commands | `/gps-tracking/commands` | `GPS.View` | `Gps.CommandView`, `Gps.CommandSend`, … |

**Module:** `GPS` (legacy key `gps-tracking`).

### 1.4 CRM / Finance / Analytics

| Screen | Route | Open | Write |
|--------|-------|------|-------|
| Customers | `/customers` | `Customer.View` | `Customer.Create`, `Customer.Update`, `Customer.Delete` |
| Payments | `/payments` | `Payment.View` | `Payment.Create`, `Payment.Update` |
| Reports | `/reports` | `Report.View` | — |
| Notifications | `/notifications` | `Notification.View` | `Notification.Manage` |
| AI | `/ai` | `Ai.View` | `Ai.Manage`, `Ai.ExecuteWrite` |

**Modules:** `CRM`, `FINANCE`, `ANALYTICS`, plus `DASHBOARD` for some menu placements of Notifications/AI.

### 1.5 Identity & org

| Screen | Route | Open |
|--------|-------|------|
| Users | `/users` | `Platform.Users.View` |
| Access Control (roles/permissions tabs) | `/platform/access-control` | `Platform.Roles.View` **OR** `Platform.Users.View` |
| Driver allowance rules | `/driver-allowance-rules` | `Driver.Manage` |
| Companies | `/platform/tenants` | `Platform.Tenants.View` |
| Branches | `/platform/branches` | `Platform.Branches.Manage` |
| Departments | `/platform/departments` | `Platform.Departments.Manage` |
| Hierarchy designer | `/platform/organization-designer` | Branches **OR** Departments **OR** Tenants.View |

### 1.6 Platform administration

| Screen | Route | Open |
|--------|-------|------|
| Platform home | `/platform` | Any of: Tenants/Roles/Users/Branches/Departments/Settings/AuditLogs/Migrations/System.Reset |
| Module Registry | `/platform/module-management` | `Platform.Tenants.View` **OR** Manage |
| Feature Management | `/platform/feature-management` | `Platform.Tenants.View` **OR** Manage |
| Menu Management | `/platform/menu-management` | `Platform.Menus.Manage` |
| Workspace Management | `/platform/workspace-management` | `Platform.Workspaces.Manage` |
| Dashboard Management | `/platform/dashboard-management` | `Platform.Dashboards.View` **OR** Manage |
| Security Center | `/platform/security-center` | `Platform.Security.View` **OR** Manage |
| Permission Coverage | `/platform/permission-coverage` | `Platform.Security.Manage` |
| Audit Center | `/platform/audit-center` | `Platform.Audit.View` **OR** `Platform.AuditLogs.View` **OR** Manage |
| Subscriptions / Plans | `/platform/subscription-management` | `Platform.Tenants.View` **OR** Manage |
| Migrations | `/platform/migrations` | `Platform.Migrations.View` **OR** Manage |
| Database Reset | `/platform/maintenance` | `Platform.System.Reset` |
| Settings | `/settings` | `Platform.Settings.View` |

---

## 2. Recommended role → screens

Based on seeded `TenantRolePermissionTemplates` + workspace hints.

| Role | Default workspace | Screens they should see (when modules enabled) |
|------|-------------------|------------------------------------------------|
| **SUPER_ADMIN** | `platform` | Platform Admin, Organization, Access, Security, Audit, Settings, Migrations — **not** Fleet/Operations until workspace switched |
| **TENANT_ADMIN** | `company` | Dashboard, Organization, Access, Analytics — **not** Fleet/Operations by default workspace |
| **FLEET_MANAGER** | `fleet` | Dashboard + **Fleet** (vehicles, drivers, GPS, fuel, maintenance, trips) |
| **DRIVER_MANAGER** | `drivers` | Dashboard + **Fleet** (drivers focus) + GPS view + Trip view + Reports |
| **DISPATCHER** | `trips` | Dashboard + **Operations** + **Fleet** + Customers + GPS (limited) |
| **ACCOUNTANT** | `finance` | Dashboard + Payments + Reports + Fuel + Maintenance view |
| **DRIVER** | `driver` | Driver app routes (`/my-trips`) — not full ERP Fleet shell |

### Demo logins (local seed)

| Email | Password | Sees Fleet sidebar? |
|-------|----------|---------------------|
| `drivermanager@sheikhtravel.com` | `Pass@123` | Yes (`drivers` workspace) |
| `dispatcher@sheikhtravel.com` | `Pass@123` | Yes (`trips` workspace) |
| `admin@sheikhtravel.com` | `Pass@123` | No by default (`platform` workspace) |

---

## 3. Full permission catalog (assignable codes)

### Platform

| Code | Typical use |
|------|-------------|
| `Platform.Dashboard.View` | Dashboard screen |
| `Platform.Users.View` / `Create` / `Edit` | Users screens / APIs |
| `Platform.Roles.View` / `Manage` | Access Control roles |
| `Platform.Tenants.View` / `Manage` | Companies, modules, plans |
| `Platform.Branches.Manage` | Branches, hierarchy |
| `Platform.Departments.Manage` | Departments, hierarchy |
| `Platform.AuditLogs.View` | Legacy audit menu |
| `Platform.Audit.View` / `Manage` | Audit Center |
| `Platform.Menus.Manage` | Menu Management |
| `Platform.Workspaces.Manage` | Workspace Management |
| `Platform.Dashboards.View` / `Manage` | Dashboard builder |
| `Platform.Security.View` / `Manage` | Security Center + Permission Coverage (Manage) |
| `Platform.Settings.View` / `Manage` | Settings |
| `Platform.Migrations.View` / `Manage` | Migrations |
| `Platform.System.Reset` | Database Reset |

### Fleet / Drivers / Maintenance / GPS

| Code | Typical use |
|------|-------------|
| `Vehicle.View` / `Create` / `Update` / `Delete` | Vehicles + fleet shell |
| `Driver.View` / `Create` / `Update` / `Delete` / `Assign` / `Manage` / `ManageStatus` / `ViewPerformance` | Drivers |
| `Maintenance.View` / `Manage` / `Request.Create` / `Request.Approve` / `WorkOrder.Manage` / `Workshop.Manage` / `Vendor.Manage` / `Report.View` | Maintenance |
| `GPS.View` | GPS screens |
| `Gps.Alert*` / `Gps.Command*` | Alerts & commands APIs |

### Operations / Finance / CRM / AI

| Code | Typical use |
|------|-------------|
| `Booking.*` | Bookings |
| `Trip.*` | Trips |
| `Route.*` | Routes |
| `Payment.*` / `Invoice.View` / `Fuel.*` | Finance / fuel |
| `Customer.*` / `Report.View` | CRM / reports |
| `Ai.View` / `Manage` / `ExecuteWrite` | AI |
| `Notification.View` / `Manage` | Notifications |

---

## 4. Module enablement checklist

| To show sidebar group | Enable Module Registry code | Legacy keys unlocked |
|-----------------------|-----------------------------|----------------------|
| Dashboard | `DASHBOARD` | `dashboard` |
| Operations | `TRAVEL` | `bookings`, `routes`, `trips` |
| Fleet | `FLEET` | `vehicles`, `drivers`, `fuel-logs`, `maintenance` |
| GPS / live tracking | `GPS` | `gps-tracking` |
| Customers | `CRM` | `customers` |
| Finance | `FINANCE` | `payments` |
| Reports / Audit menu | `ANALYTICS` | `reports`, `audit-logs` |
| Users / allowance | `ACCESS` | `users`, `driver-allowance-rules` |

---

## 5. Workspace soft focus (why screens vanish)

| Workspace key | ModuleKeys included | Hides |
|---------------|---------------------|-------|
| `platform` | platform, organization, access_control, administration | Fleet, Operations, Finance, … |
| `company` | dashboard, organization, administration, access_control, analytics | Fleet, Operations |
| `fleet` / `drivers` | fleet, dashboard | Operations (unless also in keys) |
| `trips` | operations, fleet, dashboard, customers | Platform-heavy groups |
| `finance` | finance, analytics, dashboard | Fleet/Operations |
| `home` | *(none / unrestricted)* | Soft focus off |

**Action:** If Fleet is Enabled in Module Registry but missing from sidebar, check the user’s **workspace** (not only permissions).

---

## 6. Minimum permission packs (copy for new roles)

### Pack A — Fleet operator (see Fleet screens)

```
Platform.Dashboard.View
Vehicle.View
Driver.View
GPS.View
Fuel.View
Maintenance.View
Trip.View
Notification.View
```

Optional write: `Vehicle.Create/Update`, `Driver.Create/Update/Assign`, `Fuel.Create`, `Maintenance.Manage`, `Gps.AlertView`, `Gps.CommandView`.

### Pack B — Dispatcher (see Operations + Fleet)

```
Platform.Dashboard.View
Booking.View
Trip.View
Route.View
Vehicle.View
Driver.View
GPS.View
Customer.View
Notification.View
```

Optional write: `Booking.Create/Update`, `Trip.Create/Update/Assign`, `Route.Create/Update`, `Customer.Create/Update`.

### Pack C — Accountant

```
Platform.Dashboard.View
Payment.View
Invoice.View
Report.View
Fuel.View
Maintenance.View
Maintenance.Report.View
Notification.View
```

### Pack D — Platform admin (tenant)

```
Platform.Dashboard.View
Platform.Users.View
Platform.Roles.View
Platform.Branches.Manage
Platform.Departments.Manage
Platform.Settings.View
Platform.AuditLogs.View
Platform.Security.View
Platform.Menus.Manage
Platform.Workspaces.Manage
Platform.Dashboards.View
```

Plus `Platform.Tenants.*` only for Super Admin / cross-company.

---

## 7. Implementation notes for further work

1. **Sidebar ≠ route alone** — Fixing permissions without enabling `TRAVEL`/`FLEET` or fixing workspace will not show the menu.
2. **OR guards** — Route `data.permissions: ['A','B']` means **either** A or B is enough.
3. **Write vs View** — Opening a list needs `*.View`; buttons/APIs need Create/Update/Delete (Stage 15).
4. **Super Admin** — Bypasses permission checks on API; menu still soft-filtered by **platform** workspace.
5. **Source files**
   - Routes: `Frontend/sheikhgo-erp/src/app/app-routing.module.ts`, `platform-admin.module.ts`
   - Catalogs: `Backend/SheikhTravelSystem.Application/Common/*Permissions.cs`
   - Role templates: `TenantRolePermissionTemplates.cs`
   - Workspaces: `WorkspaceRegistrySeed.cs`, `WorkspaceBuilderHandlers.RoleHint`
   - Modules: `TenantModuleCatalog.cs`

---

## 8. Checklist template (for you to fill per role)

| Role name | _______________ |
|-----------|-----------------|
| Workspace | _______________ |
| Enabled modules | ☐ DASHBOARD ☐ FLEET ☐ GPS ☐ TRAVEL ☐ CRM ☐ FINANCE ☐ ANALYTICS ☐ ACCESS |

| Screen needed? | Permission to assign |
|----------------|----------------------|
| ☐ Dashboard | `Platform.Dashboard.View` |
| ☐ Bookings | `Booking.View` (+ writes…) |
| ☐ Trips | `Trip.View` |
| ☐ Routes | `Route.View` |
| ☐ Vehicles | `Vehicle.View` |
| ☐ Drivers | `Driver.View` |
| ☐ GPS | `GPS.View` |
| ☐ Fuel | `Fuel.View` |
| ☐ Maintenance | `Maintenance.View` |
| ☐ Customers | `Customer.View` |
| ☐ Payments | `Payment.View` |
| ☐ Reports | `Report.View` |
| ☐ Users | `Platform.Users.View` |
| ☐ Access Control | `Platform.Roles.View` |
| ☐ Platform Admin | `Platform.Tenants.View` / … |

---

*Document generated from Sheikh Travel System codebase (ERP route guards, permission catalogs, role templates, workspace seeds, module catalog). Update this file when new screens or permissions are added.*
