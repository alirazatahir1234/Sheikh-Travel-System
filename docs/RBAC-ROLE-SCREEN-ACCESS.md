# SheikhGo — Complete Role-Based Access Report

**Source of truth (permissions):** [`TenantRolePermissionTemplates.cs`](../Backend/SheikhTravelSystem.Application/Common/TenantRolePermissionTemplates.cs)  
**Source of truth (ERP menus):** `PlatformMenus` (seeded in `PlatformSchemaMigration`) + Angular fallback [`menu-config.ts`](../Frontend/sheikhgo-erp/src/app/core/navigation/menu-config.ts)  
**Source of truth (Fleet app):** [`fleet_nav_config.dart`](../Frontend/sheikh-driver/lib/core/navigation/fleet_nav_config.dart) + [`auth_models.dart`](../Frontend/sheikh-driver/lib/features/auth/domain/auth_models.dart)

**Generated from codebase:** July 2026  
**Note:** Access Control UI (`/platform/access-control`) can override seeded permissions per tenant at runtime.

---

## 1. Role catalog

### Platform / tenant roles (`Roles.Code`)

| Role code | Display name | Purpose |
|-----------|--------------|---------|
| `SUPER_ADMIN` | Super Admin | Platform tenant (Id=1); all permissions |
| `TENANT_ADMIN` | Tenant Admin | Full tenant operations + user/role admin |
| `FLEET_MANAGER` | Fleet Manager | Vehicles, drivers, GPS, maintenance, reports |
| `DISPATCHER` | Dispatcher | Bookings, trips, routes, live GPS (limited commands) |
| `DRIVER_MANAGER` | Driver Manager | Seeded permission template (driver ops + trip/GPS/report view) |
| `ACCOUNTANT` | Accountant | Payments, invoices, fuel/maintenance reports |
| `DRIVER` | Driver | Field trips, GPS view, fuel, maintenance requests |

### Legacy enum (`Users.Role`) — mapped to platform codes

| Legacy | Maps to |
|--------|---------|
| Admin | `TENANT_ADMIN` |
| Dispatcher | `DISPATCHER` |
| Driver | `DRIVER` |
| Accountant | `ACCOUNTANT` |

JWT emits **both** the legacy name (`ClaimTypes.Role`) and platform codes (`role` claims) plus `permission` claims.

### Other apps

| App | Role | Notes |
|-----|------|-------|
| Customer Portal | `PortalCustomer` | Portal APIs only |
| Driver / Fleet Flutter | Same platform codes via `FleetRole` | Tab visibility from roles + permissions |

---

## 2. How screen access is decided

```text
Login
  → JWT (roles + permissions)
  → GET /api/platform/menus/me
       filters PlatformMenus by:
         1) user has PermissionCode
         2) tenant has module enabled
  → Angular sidebar shows filtered menu
  → Flutter fleet app uses FleetNavConfig + hasPermission / role checks
```

**Important limitations**

1. Angular ERP routes are **not** permission-gated (only auth + driver workspace guard). Menu hide + API `[RequirePermission]` enforce access.
2. Fallback `menu-config.ts` uses `adminOnly` = legacy role string `'Admin'` (not `TENANT_ADMIN` / `SUPER_ADMIN`).
3. Flutter Fleet app resolves a **primary nav role** for bottom tabs; More menu entries stay permission-gated.
4. AI Management (`/ai`) in ERP uses `Platform.Dashboard.View` (same as Dashboard / Notifications); Flutter AI tab is role-gated to Admin + Fleet Manager.
---

## 3. Screen ↔ permission matrix (ERP)

| Screen | Route | Required permission | SUPER_ADMIN | TENANT_ADMIN | FLEET_MANAGER | DISPATCHER | ACCOUNTANT | DRIVER | DRIVER_MANAGER* |
|--------|-------|---------------------|:-----------:|:------------:|:-------------:|:----------:|:----------:|:------:|:---------------:|
| Dashboard | `/dashboard` | `Platform.Dashboard.View` | Y | Y | Y | Y | Y | — | — |
| Notification Center | `/notifications` | `Platform.Dashboard.View` | Y | Y | Y | Y | Y | — | — |
| AI Management | `/ai` | `Platform.Dashboard.View` | Y | Y | Y | Y | Y | — | — |
| Bookings | `/bookings` | `Booking.View` | Y | Y | — | Y | — | — | — |
| Trips (ops) | `/trips`, `/trips/live` | `Trip.View` | Y | Y | — | Y | — | Y† | — |
| Routes | `/routes` | `Route.View` | Y | Y | — | Y | — | — | — |
| Vehicles | `/vehicles` | `Vehicle.View` | Y | Y | Y | Y | — | — | — |
| Fleet Dashboard | `/fleet/dashboard` | `Vehicle.View` | Y | Y | Y | Y | — | — | — |
| Assignments | `/fleet/assignments` | `Vehicle.View` | Y | Y | Y | Y | — | — | — |
| Inspections | `/fleet/inspections` | `Vehicle.View` | Y | Y | Y | Y | — | — | — |
| Compliance | `/fleet/compliance` | `Vehicle.View` | Y | Y | Y | Y | — | — | — |
| Drivers | `/drivers` | `Driver.View` | Y | Y | Y | Y | — | — | — |
| Live / Fleet Tracking | `/gps-tracking`, `/gps-tracking/live` | `GPS.View` | Y | Y | Y | Y | — | Y† | — |
| GPS Trips | `/gps-tracking/trips` | `GPS.View` | Y | Y | Y | Y | — | Y† | — |
| Devices / Trackers | `/gps-tracking/devices` | `GPS.View` | Y | Y | Y | Y | — | Y† | — |
| Geofences | `/gps-tracking/geofences` | `GPS.View` | Y | Y | Y | Y | — | Y† | — |
| Maintenance | `/maintenance`, `/fleet/maintenance` | `Maintenance.View` | Y | Y | Y | — | Y | Y† | — |
| Service Records | `/maintenance/service-records` | `Maintenance.View` | Y | Y | Y | — | Y | Y† | — |
| Fuel Logs | `/fuel-logs` | `Fuel.View` | Y | Y | Y | — | Y | Y† | — |
| Customers | `/customers` | `Customer.View` | Y | Y | — | Y | — | — | — |
| Payments / Invoices | `/payments` | `Payment.View` / `Invoice.View` | Y | Y | — | — | Y | — | — |
| Reports | `/reports` | `Report.View` | Y | Y | Y | — | Y | — | — |
| Audit Logs | `/audit-logs` | `Platform.AuditLogs.View` | Y | Y | — | — | — | — | — |
| Users | `/users` | `Platform.Users.View` | Y | Y | — | — | — | — | — |
| Roles | `/platform/roles` | `Platform.Roles.View` | Y | Y | — | — | — | — | — |
| Access Control | `/platform/access-control` | `Platform.Roles.Manage` | Y | Y | — | — | — | — | — |
| Allowance Rules | `/driver-allowance-rules` | `Platform.Roles.Manage` | Y | Y | — | — | — | — | — |
| Tenants | `/platform/tenants` | `Platform.Tenants.View` | Y | —‡ | — | — | — | — | — |
| Branches | `/platform/branches` | `Platform.Branches.Manage` | Y | Y | — | — | — | — | — |
| Departments | `/platform/departments` | `Platform.Departments.Manage` | Y | Y | — | — | — | — | — |
| Settings | `/settings` | `Platform.Settings.View` / Manage | Y | —§ | — | — | — | — | — |
| Module / Plans / Migrations | `/platform/*` | Platform admin perms | Y | — | — | — | — | — | — |

\* `DRIVER_MANAGER` uses the seeded template in §4 / §8 (Drivers, Trip/GPS/Report view). ERP menus still follow permission codes.  
† Driver has the permission but ERP **workspace guard** restricts legacy `Driver` users to `/my-trips` and `/profile` only. Drivers should use the Flutter Driver/Fleet app.  
‡ `TENANT_ADMIN` template does **not** include `Platform.Tenants.*` (platform-only).  
§ `TENANT_ADMIN` template includes `Platform.Menus.Manage` but **not** `Platform.Settings.View` / `Manage` in the seed list — Settings menu may require Access Control override or SUPER_ADMIN.

Legend: **Y** = included in seed template · **—** = not in seed template.

---

## 4. Role → screens (recommended operational view)

### SUPER_ADMIN
**Needs:** Everything — all ERP screens, platform tenants/modules/plans/migrations, Access Control, AI, reports.

### TENANT_ADMIN
**Needs:**
- Dashboard, Notifications, AI Management
- Bookings, Trips, Dispatch Board, Routes
- Vehicles, Drivers, Assignments, Inspections, Compliance
- Live Tracking, GPS Trips, Devices, Geofences
- Maintenance, Service Records, Fuel
- Customers, Payments, Reports, Audit Logs
- Users, Roles, Access Control, Allowance Rules
- Branches, Departments
- Tenant settings (if Settings permission granted)

**Does not need (platform-only):** Tenants list, Module Management, Plans/Billing, Migration Manager (unless also SUPER_ADMIN).

### FLEET_MANAGER
**Needs:**
- Dashboard, Notifications, AI Management
- Vehicles (CRUD), Drivers (CRUD/assign/status), Assignments, Inspections, Compliance
- Live Tracking, GPS Trips, Devices, Geofences (+ full GPS commands)
- Maintenance hub (approve, work orders, workshops, vendors), Service Records, Fuel
- Fleet Reports

**Does not need:** Bookings create/manage, Routes, Customers, Payments, Users/Roles, Platform org screens.

### DISPATCHER
**Needs:**
- Dashboard, Notifications, AI (via dashboard permission)
- Bookings, Trips, Dispatch Board, Routes
- Vehicles (view), Drivers (view), Assignments (view)
- Live Tracking / Fleet Tracking (view + send/position GPS commands)
- Customers

**Does not need:** Vehicle/driver CRUD, Maintenance manage, Fuel, Payments, Reports, Users/Roles, Platform admin.

### ACCOUNTANT
**Needs:**
- Dashboard, Notifications
- Payments, Invoices
- Fuel (view), Maintenance (view + maintenance reports)
- Reports

**Does not need:** Bookings ops, Live GPS commands, Driver/Vehicle CRUD, Users/Roles, Platform admin.

### DRIVER (ERP web)
**Needs (permission-wise):** Trip view, GPS view, Fuel view, Maintenance request/view.  
**Actual ERP access:** Guarded to **My Trips** + **Profile** only. Prefer Flutter Driver app for day-to-day work.

### DRIVER_MANAGER
**Needs (intended product role):** Drivers list, assignments, driver performance, related fleet screens.  
**Seeded template:** same starter set below (applied via `TenantRolePermissionTemplates.DriverManager` + `DriverManagerRoleTemplateMigration`).

```text
Platform.Dashboard.View
Driver.View, Driver.Create, Driver.Update, Driver.Assign, Driver.Manage, Driver.ManageStatus, Driver.ViewPerformance
Vehicle.View
GPS.View
Trip.View
Report.View
```

**Fleet demo login:** `drivermanager@sheikhtravel.com` / `Pass@123`

---

## 5. Action-level permissions (beyond menu visibility)

These gate **buttons/API**, not just menu items:

| Action area | Permissions | Typical roles |
|-------------|-------------|---------------|
| Create booking | `Booking.Create` | TENANT_ADMIN, DISPATCHER |
| Vehicle delete | `Vehicle.Delete` | TENANT_ADMIN, FLEET_MANAGER |
| Driver assign / status | `Driver.Assign`, `Driver.ManageStatus` | TENANT_ADMIN, FLEET_MANAGER |
| Maintenance approve / WO | `Maintenance.Request.Approve`, `Maintenance.WorkOrder.Manage` | TENANT_ADMIN, FLEET_MANAGER |
| GPS engine cutoff / relay / etc. | `Gps.Command*` (full set) | TENANT_ADMIN, FLEET_MANAGER |
| GPS view / send / position only | `Gps.CommandView`, `Send`, `PositionRequest` | + DISPATCHER |
| Manage users / roles | `Platform.Users.*`, `Platform.Roles.*` | SUPER_ADMIN, TENANT_ADMIN |

---

## 6. Fleet / Driver Flutter app (screen access)

Bottom navigation is **role-first** via `FleetSession.primaryNavRole` (priority: SUPER_ADMIN → TENANT_ADMIN → FLEET_MANAGER → DRIVER_MANAGER → DISPATCHER → ACCOUNTANT → DRIVER).

### 6.1 Role bottom-nav shells

| Primary role | Bottom tabs |
|--------------|-------------|
| DRIVER | Dashboard, Trips, Tracking, Inbox, Profile |
| FLEET_MANAGER / TENANT_ADMIN / SUPER_ADMIN | Dashboard, Fleet, Trips, More (+ AI FAB) |
| DRIVER_MANAGER | Dashboard, Drivers, Trips, More |
| DISPATCHER | Dashboard, Bookings, Trips, Map, More |
| ACCOUNTANT | Dashboard, Finance*, Reports, Inbox, More |

\* `/finance` and `/users` remain “coming soon” stubs until later phases. Bookings is live (Phase 4 Dispatcher). Driver Manager Drivers hub is live (Phase 5). AI Copilot is a **floating FAB** for Admin/FM (not a bottom tab).

### Command Dashboard role mapping (PRD names → JWT)

| PRD name | JWT / DashboardRole |
|----------|---------------------|
| Owner | `TENANT_ADMIN` / `SUPER_ADMIN` |
| Fleet Manager | `FLEET_MANAGER` |
| Operations Manager | `FLEET_MANAGER` (same widgets) |
| Dispatcher | `DISPATCHER` |
| Supervisor | `DRIVER_MANAGER` |
| Maintenance Manager | `FLEET_MANAGER` + `Maintenance.View` gates |
| Driver | `DRIVER` |

Dashboard widgets are composed via `DashboardLayoutRegistry` + `DashboardVisibility` (health, KPIs, map summary vs interactive, role-filtered alerts, search, attention vehicles).

### 6.2 Screen / permission matrix

| Screen / tab | Route | Visibility rule | SUPER_ADMIN | TENANT_ADMIN | FLEET_MANAGER | DRIVER_MANAGER | DISPATCHER | ACCOUNTANT | DRIVER |
|--------------|-------|-----------------|:-----------:|:------------:|:-------------:|:--------------:|:----------:|:----------:|:------:|
| Dashboard | `/dashboard` | Always | Y | Y | Y | Y | Y | Y | Y |
| Fleet hub | `/fleet` | Ops role **or** `GPS.View` / `Vehicle.View` | Y | Y | Y | Y | Y | — | — |
| Fleet live map | `/fleet/map` | Same as Fleet tab | Y | Y | Y | Y | Y | — | — |
| Trips | `/trips` | `Trip.View` or driver session | Y | Y | Y | Y | Y | — | Y |
| AI Copilot | `/ai` | FAB for SUPER_ADMIN / TENANT_ADMIN / FLEET_MANAGER (`canSeeAiTab`) | Y | Y | Y | — | — | — | — |
| Bookings queue / detail | `/bookings` | `Booking.View` | Y | Y | — | — | Y | — | — |
| Finance (stub) | `/finance` | Payment/Invoice/Report view | Y | Y | — | — | — | Y | — |
| Users (stub) | `/users` | `Platform.Users.View` | Y | Y | — | — | — | — | — |
| More | `/more` | Staff shells / always for ops | Y | Y | Y | Y | Y | Y | —† |
| My tracking | `/live` | Driver session only | — | — | — | — | — | — | Y |
| Drivers list / detail | `/more/drivers` | Not driver-only + `Driver.View` | Y | Y | Y | Y | Y | — | — |
| Alerts | `/alerts` | Not driver-only + `GPS.View` | Y | Y | Y | Y | Y | — | — |
| Maintenance | `/more/maintenance` | Not driver-only + `Maintenance.View` | Y | Y | Y | — | — | Y | — |
| Fuel | `/fuel` | `Fuel.View` or driver session | Y | Y | Y | — | — | Y | Y |
| Reports | `/more/reports` | Not driver-only + `Report.View` | Y | Y | Y | Y | — | Y | — |
| Documents | `/documents` | Driver session **or** `Vehicle.View` | Y | Y | Y | Y | Y | — | Y |
| Attendance / Inspection / Earnings / Timeline | various | Driver session only | — | — | — | — | — | — | Y |
| Notifications / Profile / Settings | various | Always | Y | Y | Y | Y | Y | Y | Y |

† Driver shell uses Profile instead of More; Settings remain reachable from Profile.

---

## 7. Tenant module entitlement (second gate)

Even with a permission, the screen stays hidden if the tenant module is disabled:

| Module code | Unlocks menu keys |
|-------------|-------------------|
| DASHBOARD | `dashboard` |
| FLEET | `vehicles`, `drivers`, `fuel-logs`, `maintenance` |
| GPS | `gps-tracking` |
| TRAVEL | `bookings`, `routes`, `trips` |
| CRM | `customers` |
| FINANCE | `payments` |
| ANALYTICS | `reports`, `audit-logs` |
| ACCESS | `users`, `driver-allowance-rules` |

---

## 8. Seeded permission sets (reference)

### TENANT_ADMIN
`Platform.Dashboard.View`, Users View/Create/Edit, Roles View/Manage, Branches/Departments Manage, AuditLogs, Menus Manage · Booking View/Create · Trip/Route View · full Vehicle/Driver · GPS + all Gps.Command* · Fuel · full Maintenance · Customer · Payment · Invoice · Report

### FLEET_MANAGER
Dashboard · Trip.View · full Vehicle/Driver · GPS + all Gps.Command* · Fuel · full Maintenance · Report

### DRIVER_MANAGER
Dashboard · Driver View/Create/Update/Assign/Manage/ManageStatus/ViewPerformance · Vehicle.View · GPS.View · Trip.View · Report.View

### DISPATCHER
Dashboard · Booking View/Create · Trip/Route View · Vehicle/Driver View · GPS View · Customer · Gps.CommandView/Send/PositionRequest

### ACCOUNTANT
Dashboard · Payment · Invoice · Report · Fuel · Maintenance View + Maintenance.Report.View

### DRIVER
Trip.View · GPS.View · Fuel.View · Maintenance.Request.Create · Maintenance.View · Gps.CommandView

### SUPER_ADMIN
All rows in `Permissions` table (seeded for platform tenant).

---

## 9. Gaps & recommendations

| Gap | Recommendation |
|-----|----------------|
| ERP routes not permission-guarded | Add Angular `permissionGuard` for sensitive routes (`/users`, `/platform/*`, `/settings`) |
| Fallback menu `adminOnly` checks `'Admin'` only | Also accept `SUPER_ADMIN` / `TENANT_ADMIN` |
| `TENANT_ADMIN` missing `Platform.Settings.*` | Add Settings.View/Manage to TenantAdmin template if tenant settings should be available |
| AI gated only by Dashboard.View in ERP | Introduce `Ai.Chat.View` / `Ai.Manage` if AI should be role-restricted |
| Flutter finance / users | Replace coming-soon stubs in Phases 6 / 7 |
| Driver Manager training module | Not in API yet — deferred |

**Resolved:** `DRIVER_MANAGER` template seeded; `FLEET_MANAGER` includes `Trip.View`; Fleet app uses role-specific bottom nav; Dispatcher bookings live; Driver Manager hub (license alerts, ranking, performance/attendance/violations/docs, assign vehicle) live.

**Master gap map:** [FLEET-MASTER-GAP-ROADMAP.md](./FLEET-MASTER-GAP-ROADMAP.md) (phases, sprints, next build order).

---

## 10. Quick checklist — “Who needs which screen?”

| If you want this screen… | Minimum role to assign (seeded) |
|--------------------------|----------------------------------|
| Full company ERP | `TENANT_ADMIN` |
| Fleet ops only (vehicles/GPS/maintenance) | `FLEET_MANAGER` |
| Booking / dispatch day board | `DISPATCHER` |
| Finance & fuel reports | `ACCOUNTANT` |
| Platform multi-tenant / migrations | `SUPER_ADMIN` |
| Field driver work | `DRIVER` + Flutter Driver app |
| Dedicated driver supervisor | `DRIVER_MANAGER` (seeded template) + Fleet app |

---

*Document maintained from code templates. When Access Control changes a tenant’s RolePermissions, that tenant’s live menu may differ from this report.*
