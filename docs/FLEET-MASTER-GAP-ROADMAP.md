# SheikhGo Fleet — Master Gap Roadmap

**Audited:** 22 Jul 2026  
**Scope:** Flutter `Frontend/sheikh-driver` vs the role-first Fleet vision (same APIs / roles / permissions as SheikhGo ERP).  
**Sources:** codebase, `TenantRolePermissionTemplates.cs`, [RBAC-ROLE-SCREEN-ACCESS.md](./RBAC-ROLE-SCREEN-ACCESS.md).

This is a **planning document only** (option 3). No feature build in this pass.

---

## Snapshot

| Phase | Status |
|-------|--------|
| 1 Foundation | **Done** |
| 2 Driver | **Done** |
| 3 Fleet Manager | **Partial** |
| 4 Dispatcher | **Partial** (bookings live; board/routes thin) |
| 5 Driver Manager | **Partial** (hub live; training / writes deferred) |
| 6 Accountant | **Missing** (`/finance` stub) |
| 7 Tenant Admin | **Missing** (`/users` stub; shares FM shell) |
| 8 Super Admin | **Missing** (ERP-only platform) |

**Already shipped in recent sessions:** RBAC shells, Fleet Manager vehicle depth, Dispatcher bookings, Driver Manager hub, **Fleet Command Dashboard** (role-based widgets + AI FAB).

### Command Dashboard (2026-07-22)

- One `/dashboard` with widget registry per role (Owner map summary vs FM interactive map; Dispatcher without health).
- Universal search sheet; attention vehicles; enriched health % + AI blurb.
- Bottom nav for FM/Admin: Dashboard · Fleet · Trips · More; AI via FAB.
- Deferred: dedicated Owner/Maint Manager JWT roles, Drivers tab for FM, vehicle-detail tab RBAC, drawer.

---

## 1. Phase detail

### Phase 1 — Foundation — Done

**Exists:** Staff email + driver phone auth, JWT refresh, roles/permissions on `FleetSession`, role-first bottom nav, ERP teal theme, EN/AR i18n, Hive offline outbox, SignalR, FCM, biometric lock, AI chat client.

**Gaps:** No company / tenant picker (slug via config). Splash is stock Flutter, not branded product splash.

### Phase 2 — Driver — Done

**Exists:** Dashboard · Trips · Tracking · Inbox · Profile. Trips lifecycle, live GPS, attendance, fuel (+ offline), inspection, documents, earnings, timeline, SOS, collect payment, notifications, settings, offline queue.

**Gaps:** Vision “100%” polish only; driver AI not gated (and not a priority).

### Phase 3 — Fleet Manager — Partial

**Exists:** Dashboard · Fleet · Trips · AI · More. Fleet hub/KPIs, live map + geofence circles, vehicle workspace (docs / maint / fuel / GPS / history playback / commands), drivers, ops trips, alerts, maintenance hub (read), staff fuel, reports hub, AI Copilot.

**Gaps:** Maintenance write (approve / WO / workshops / vendors / calendar); vehicle CRUD; map clustering / traffic / satellite / heatmap; report PDF/Excel export; fuel theft analytics UI.

### Phase 4 — Dispatcher — Partial

**Exists:** Dashboard · Bookings · Trips · Map · More. Bookings queue/detail, assign driver/vehicle, confirm, create trip from booking, ops trip assign/status, live map, dispatcher dashboard widgets.

**Gaps:** Dedicated dispatch board; routes UI (`Route.View` seeded, unused); create-booking flow; customer contact / delay / ETA-first UX.

### Phase 5 — Driver Manager — Partial

**Exists:** Dashboard · Drivers · Trips · More. Seeded `DRIVER_MANAGER` template. License expiry chips, ranking, detail performance / violations / attendance / docs (read), assign/unassign vehicle, status manage.

**Gaps:** Training (no API); manager **write** attendance / violations (`POST` exists for violations, Flutter GET-only).

### Phase 6 — Accountant — Missing

**Exists:** Shell wired (Finance · Reports · Inbox · More). Reuses reports, fuel, maintenance view, accountant dashboard widgets.

**Gap:** `/finance` = Coming Soon. No payments / invoices / expenses / export screens.

### Phase 7 — Tenant Admin — Missing

**Exists:** Permissions for users/roles/branches/departments. Shares **Fleet Manager** bottom nav (not vision Operations / Users shell).

**Gap:** `/users` = Coming Soon. No roles / branches / departments / notification templates / AI settings admin UI.

### Phase 8 — Super Admin — Missing

**Exists:** Platform banner on dashboard; full permissions in seed.

**Gap:** No tenants / plans / modules / billing / migrations / platform settings in Flutter (ERP-only).

---

## 2. Coming soon stubs

| Route | Title | Label | Guard |
|-------|-------|-------|-------|
| `/finance` | Finance | Phase 6 — Accountant | Payment / Invoice / Report view |
| `/users` | Users | Phase 7 — Tenant Admin | `Platform.Users.View` |

Bookings is **live** (not a stub).

---

## 3. Role shells today

| Primary role | Bottom tabs |
|--------------|-------------|
| DRIVER | Dashboard, Trips, Tracking, Inbox, Profile |
| FLEET_MANAGER / TENANT_ADMIN / SUPER_ADMIN | Dashboard, Fleet, Trips, AI, More |
| DRIVER_MANAGER | Dashboard, Drivers, Trips, More |
| DISPATCHER | Dashboard, Bookings, Trips, Map, More |
| ACCOUNTANT | Dashboard, Finance*, Reports, Inbox, More |

\* Finance route is stub until Phase 6 build.

Priority: `SUPER_ADMIN → TENANT_ADMIN → FLEET_MANAGER → DRIVER_MANAGER → DISPATCHER → ACCOUNTANT → DRIVER`.

---

## 4. Vision sprint mapping

| Sprint | Vision items | Status |
|--------|--------------|--------|
| 1 | Splash, Login, Company selection, Dashboard, Notifications, Profile, Settings | **Partial** — company selection missing; splash thin |
| 2 | Vehicles, detail, Live map, GPS | **Done** (core) — advanced map layers missing |
| 3 | Drivers, detail, Attendance, Performance | **Done** (read) — manager write attendance missing |
| 4 | Trips, Dispatch, Booking, Route | **Partial** — board + routes missing |
| 5 | Maintenance, Fuel, Documents | **Partial** — maintenance read-only |
| 6 | Reports, Analytics | **Partial** — no export |
| 7 | AI Copilot | **Done** for Admin / FM — not Dispatcher / Accountant |
| 8 | Users, Roles, Branches, Departments | **Missing** |

---

## 5. Recommended next build order

| # | Slice | Why | Effort |
|---|-------|-----|--------|
| **1** | **Accountant Finance hub** — replace `/finance`: payments, invoices, link reports/fuel/maint | Unlocks Phase 6 shell; ERP APIs exist | **M** |
| **2** | **Dispatcher polish** — create booking + routes list + light dispatch board | Closes Phase 4 without new backend | **M** |
| **3** | **Maintenance write actions** — approve request, create/update WO | Biggest FM hole; perms seeded | **M** |
| **4** | **Driver Manager writes** — create violation + staff attendance entry | Finishes Phase 5 without Training | **S** |
| **5** | **Tenant Admin Users** — replace `/users`; list/create/edit (roles later) | Starts Phase 7 | **L** |

Defer Super Admin mobile (tenants/plans/migrations) until ERP platform admin is the bottleneck — high effort, low field ROI.

---

## 6. Deferred / known gaps

| Item | Notes |
|------|--------|
| Training module | No backend domain |
| Manager write attendance / violations | API partial; Flutter GET-only |
| AI for Dispatcher / Accountant | `canSeeAiTab` is Admin/FM only |
| Finance + Users product screens | Router stubs |
| Tenant / Super Admin dedicated shells | Admins share FM tabs |
| Maintenance calendar / workshops / vendors | Hub is read |
| Routes UI / dispatch board | Seeded perms; no Flutter feature |
| Report export (PDF/Excel) | Tables only |
| Company / multi-tenant picker | Config slug |
| Map premium layers | Cluster / traffic / satellite / heatmap |

---

## 7. Permission template snapshot

| Role | Seeded (high level) |
|------|---------------------|
| TENANT_ADMIN | Full tenant ops + users/roles/branches + booking/trip/route + fleet + GPS commands + fuel/maint + payment/invoice/report |
| FLEET_MANAGER | Dashboard, trip view, full vehicle/driver, GPS + commands, fuel, full maintenance, report |
| DRIVER_MANAGER | Driver ops + assign/status/performance; vehicle/GPS/trip/report **view** |
| DISPATCHER | Booking view/create, trip/route view, vehicle/driver view, GPS + limited commands |
| ACCOUNTANT | Payment, invoice, report, fuel, maintenance view + maint report |
| DRIVER | Trip/GPS/fuel view, maintenance request, Gps.CommandView |
| SUPER_ADMIN | All (platform tenant) |

---

## 8. How to use this doc

Reply with a number from §5 (e.g. `start 1` = Accountant Finance), or a combo (`1 then 3`). Each slice should become a concrete implementation plan before coding.

Related: [RBAC-ROLE-SCREEN-ACCESS.md](./RBAC-ROLE-SCREEN-ACCESS.md), [IMPLEMENTATION-STATUS.md](./IMPLEMENTATION-STATUS.md).

*Runtime Access Control overrides may differ per tenant.*
