# SheikhGo — Super Admin Platform Audit

Nov 16, 2026 · @Ali Raza Tahir · Read-only audit of https://www.sheikhgo.com as SUPER\_ADMIN

## Executive summary

SheikhGo is architecturally a multi-tenant platform but operationally a single-tenant app: as SUPER\_ADMIN you cannot see all companies in one place, and three platform screens are entirely unusable because they ask for a tenant switcher that does not exist in the UI.

48 findings across 45 pages. 8 are P0 (fix before onboarding any paying company), 14 are P1, 19 P2, 7 P3.

The eight that matter most:

1. **Compliance module leaks across tenants.** With Hannan Khokhar Travels selected (1 driver), `/fleet/compliance` lists 39 documents belonging to \~13 drivers from other companies. The page has no company selector at all.
2. **Money is collected but never invoiced.** `/invoices` shows *No invoices found* and PKR 0 billed, while `/payments` holds 9 records totalling PKR 39,824. There is no AR, no tax document, no billing trail.
3. **Payments are not linked to bookings.** The Booking column is empty on all 9 payment rows — no payment can be reconciled to a trip or customer.
4. **GPS reports a vehicle as online when its last fix is 5 hours old.** `/gps-tracking/devices` shows tracker 862292058472119 as Online, ignition ON, 15 km/h — all marked *(cached)*, last seen 5h ago. GPS reliability is the product's core promise.
5. **The SUPER\_ADMIN role carries 0 permissions.** Access Control lists it as Active with 0 permissions and 0 users, while your effective permission count is 119. Access is coming from a hard-coded bypass, not from the RBAC tables — unauditable and fragile.
6. **There is no working audit trail.** Audit Center returns zero rows and is blocked behind the missing tenant switcher, even though the audit level is set to *Always*.
7. **MFA does not exist.** Security Center carries an MFA toggle labelled "Flag only in foundation — no MFA challenge". Trusted devices, password history and IP restriction are similarly display-only. The console implies controls it does not enforce.
8. **The production API runs on a raw IP via sslip.io** (`https://20-174-1-230.sslip.io/api/...`) — no owned domain, no WAF, and the IP is baked into the web and mobile clients.

The good news: zero JavaScript console errors across the whole sweep, every API call returned 200, SignalR is connected, Traccar v6.14.5 is live, and all 171 schema migrations are applied. The platform is stable. The problems are tenant scoping, financial completeness, and controls that look real but are not.

## Scope and method

45 routes were opened in the live production app at www.sheikhgo.com, signed in as System Admin / SUPER\_ADMIN, on 16 November 2026. Nothing was created, edited, submitted or deleted — the sweep was strictly read-only.

For each route I captured the rendered content, KPI card values, table rows, browser console output and the network calls the page made. Findings below quote what the screens actually show; where two screens disagree, both numbers are given.

| Area | Routes checked |
| --- | --- |
| Platform & Organization | /platform, /platform/tenants, organization-designer, branches, departments, module-management, subscription-management, migrations, maintenance, menu-management, workspace-management, dashboard-management, /settings |
| Access Control & Security | /platform/access-control (Users, Roles, Permissions, Data Scope, Access Policies, Role Templates), /users, /platform/security-center, /platform/audit-center, /audit-logs, /driver-allowance-rules |
| Fleet | /vehicles, /drivers, /fleet/assignments, /maintenance, /fleet/inspections, /fleet/compliance, /fuel-logs, /fleet/ev |
| Operations | /bookings, /trips, /routes |
| GPS | /gps-tracking/live, /devices, /analytics, /driver-performance, /client-tracking-links, /alerts, /platform/gps-control-center |
| Finance & CRM | /payments, /invoices, /settlements, /customers, /reports |
| Other | /dashboard, /notifications, /ai, /mcp, /rental (+ rates, availability), /whatsapp (+ webhook-logs), /website (+ pages, legal, contact-requests, demo-requests) |

What this audit could **not** establish, because it would require writing data or probing the API directly:

- Whether the backend enforces tenant isolation at the query level, or only in the UI. The compliance leak (SG-01) is evidence that at least one endpoint does not, but confirming the blast radius needs a server-side review of the tenant filter in each repository.
- Whether the public website's contact and demo forms actually post. Both inboxes are empty; that is consistent with either no traffic or a broken form.
- Whether the 1,748 failed notifications share one root cause. The UI exposes the count but not the failure reason.
- Mobile (Flutter driver app) behaviour. Not in scope here.

## What a super admin should see — and what SheikhGo gives you

In a multi-tenant fleet ERP the super admin is a *platform operator*, not a bigger tenant admin. The operator needs one global context switch, one cross-company view of every entity, and one platform-wide audit trail. SheikhGo has none of these three; instead it has four different, unsynchronised scoping mechanisms.

| Capability a platform operator needs | SheikhGo today | Gap |
| --- | --- | --- |
| One global tenant switcher in the header | Header has search, clock, help, notifications, profile menu only. Profile menu = Profile / Settings / Logout | **Missing.** Three screens explicitly instruct you to use it |
| "All companies" aggregate view of any entity | No module offers an All-companies option. Every list is one company at a time | **Missing** |
| Cross-company user directory | /users shows 5 users (one tenant). Companies page counts 9 active users platform-wide | 4 users invisible |
| Platform-wide audit trail | Audit Center returns 0 rows and is gated on the missing switcher | **Missing** |
| Impersonate / act-as a tenant admin, logged | No impersonation anywhere | **Missing** |
| Platform health: queue depths, failed jobs, integration status | Partially: GPS Control Center shows 2 queued / 6 failed commands. No equivalent for notifications, webhooks or billing | Partial |
| Billing and revenue across all tenants | Companies page shows one figure (Monthly Revenue PKR 35k) that does not reconcile with per-tenant subscriptions | Unreliable |
| Cross-tenant data isolation you can prove | Compliance page returns other tenants' drivers | **Broken** |

### The four scoping mechanisms

This is the root cause of most of the visibility findings. The same question — "which company am I looking at?" — is answered four different ways, and the four do not talk to each other.

1. **Inline company switcher** — /vehicles and /drivers show a banner: *Viewing vehicles of Hannan Khokhar Travels · Change company · Back to my company*.
2. **"Select Tenant" dropdown** — /platform/access-control, /platform/module-management, /platform/subscription-management. Lists only 3 of the 5 companies.
3. **GPS scope chips** — the whole /gps-tracking area carries its own *Company: Sheikh Travel · Vehicle: Ali (BWY 217) · Clear Scope* bar, independent of the others.
4. **No switcher at all** — /maintenance, /fleet/compliance, /fleet/inspections, /platform/branches, /platform/departments, /users, /settlements, /rental, /reports. These are silently pinned to one tenant, and not always the same one.

During this audit, mechanism 1 was set to Hannan Khokhar Travels while mechanism 3 was set to Sheikh Travel, and /maintenance announced *"Operational oversight for Sheikh Travel Group's active fleet"* — three different companies in context simultaneously. Any number you read off one screen cannot be safely compared with a number on another.

## Company records and tenant isolation

Five companies exist. The Companies page (/platform/tenants) is the one place that shows all of them, and even there the header KPI says *Active Companies 3* while five rows render.

| Company | Code | Type | Plan | Users | Health | Modules |
| --- | --- | --- | --- | --- | --- | --- |
| Sheikh Travel | SK-0001 | Travel Agency | Enterprise | 5 / 100 | **Expired** | 22 |
| Vision Factory | SK-0002 | Corporate Transport | Enterprise | 1 / 10 | **Suspended** | 20 |
| Hannan Khokhar Travels | SK-0005 | Fleet Operator | Starter | 1 / 10 | Trial Ending | 14 |
| Chand Travels | CHAND\_TRAVELS | *(blank)* | Starter | 2 *(no limit)* | Healthy | 19 |
| A Ali | ALI-BWY217 | Fleet Owner | Starter | 0 / 5 | **Suspended** | 6 |

### SG-01 — Compliance data crosses tenant boundaries (P0)

With Hannan Khokhar Travels as the active company, /drivers returns exactly one driver: Munna Driver. On the same context, /fleet/compliance renders 39 documents belonging to roughly 13 people — Ali Tahir, Ali Raza Tahir, Taqi Raza, Warda Tahir, Muhammad Tahir, Asghar Ali, Amjad Ahmed, "gg gg" and others — none of whom appear in that company's driver roster. The page carries no company selector, so this is not a mis-set filter.

The likely cause is a missing tenant predicate on the compliance-documents query. The same pattern should be checked on every repository that joins to Drivers or Vehicles, because one missing `WHERE TenantId = @tenantId` is rarely alone. Until it is verified, SheikhGo cannot honestly tell a prospect that their data is isolated from other operators on the platform.

### SG-11 — Suspended companies vanish from the tenant selector (P1)

The *Select Tenant* dropdown on Access Control, Module Registry and Subscription Management lists only Chand Travels, Hannan Khokhar Travels and Sheikh Travel. A Ali and Vision Factory — both Suspended — are absent. So the moment a company is suspended you lose the ability to inspect its users, change its modules, or renew its plan from the platform console. Suspension becomes a one-way door.

### SG-42 — User administration is tenant-scoped (P1)

/users reports Total 5 / Active 5. The Companies page reports 9 active users across the platform. Four users exist whom the super admin cannot list, search, suspend or reset from anywhere in the UI.

### SG-47 — Platform revenue does not reconcile (P2)

Companies page: *Monthly Revenue PKR 35k*. Hannan Khokhar Travels alone — a Starter plan — shows a yearly amount of PKR 1.0M and a legacy monthly amount of PKR 83k on its subscription screen. Two Enterprise tenants exist above it. Whatever the 35k figure is measuring, it is not the sum of the plans on file.

### SG-43 — Active Companies KPI undercounts (P3)

The card reads 3; the table lists 5. If the intent is "active excluding suspended", the label should say so, and Expired (Sheikh Travel) should be defined one way or the other — it is currently counted as active while its subscription has lapsed, and its 5 users still have access.

## Dashboard

The dashboard is the screen a buyer sees first, and almost every number on it is either contradicted elsewhere in the app or computed over a window its label does not match.

| Card | Shows | Problem |
| --- | --- | --- |
| Total revenue | PKR 4,200, *vs prior week* | Only the one payment with status **Paid** is counted. PKR 35,624 sitting in *PartiallyPaid* rows is ignored, and the payment is dated 7 May — not last week |
| Fuel spend | PKR 3,130, *Tracked lifetime* | All-time figure, sat next to a weekly revenue figure |
| Net profit | PKR −24,330, *revenue − fuel − maintenance* | 4,200 − 3,130 = +1,070. To reach −24,330 the formula is subtracting roughly PKR 25,400 of maintenance cost that the Maintenance module reports as **PKR 0**. The three inputs also span three different time windows |
| Fleet vehicles | 2, *0% used utilization* | /vehicles and /fleet/assignments both show **100% utilization** for the same fleet |
| Client billed / Collected / Outstanding AR | PKR 0 / 0 / 0 | Correct given zero invoices — and therefore a direct symptom of SG-02 |
| Bookings by status | *Last 30 days* — 5 bookings | All 5 bookings were created between May and October. The API was called with `fromDate=2026-10-17&toDate=2026-11-16`; the chart ignores the window it asked for |
| Vehicle tracking | 2 online, 0 moving, 1 idle, 1 parked | The Live Map reports 1 moving, 0 idle for the same two vehicles at the same moment |
| Notifications | 1,608 unread of 3,101 — Email 588, SMS 363, Push 472, Browser 508, WhatsApp 0, **Failed 1,748** | Channel counts sum to 1,931, not 3,101. 56% of all notifications failed |
| Operations alerts | 14 active · 14 new · all Warning | None has ever been acknowledged |
| Document expiry alert | *Compliance documents nearing expiry · 1 vehicle* | /fleet/compliance reports **Expiring soon: 0** and every document as UNKNOWN with no expiry date. The alert and the module disagree |

Two structural points behind the table. First, the KPI row mixes *this week*, *last 30 days* and *lifetime* without labelling which is which, so Net profit is not a number anyone should act on. Second, the greeting reads "Good Afternoon, Admin!" — the signed-in user's name is available (System Admin) and the company name is not shown anywhere on the dashboard, which matters once a super admin can be in several companies' context.

## Fleet Management

**Vehicles** — one of only two modules with a working company switcher. For Hannan Khokhar Travels it shows 1 asset (ST-FLT-2026-1136, Suzuki Every 2017), status ON ROUTE with an ENGINE WARN badge, odometer **0 km**, SIM blank. Odometer is never populated from telemetry, so every distance-based metric downstream (service intervals, km/L, allowance rules keyed on distance) has no odometer to work from. Utilization reads 100% off a single vehicle.

**Drivers** — also company-switchable. One driver, Munna Driver, and the KPI row contradicts itself: *Active Drivers 1* and *Suspended 1* describe the same person. Worse, that suspended driver is the one attached to a vehicle currently ON ROUTE. Nothing in the system blocks dispatching a suspended driver. Branch and Department are blank for every driver and every user in the platform.

**Vehicle identity is inconsistent.** The same asset appears as `ST-FLT-2026-1136`, as plate `BTB-262` on the driver record, as `BWY 217` in GPS, and as the vehicle literally named **"Ali"** in Trips, Rental and Maintenance dropdowns. A person's name is being used as a vehicle name in production.

### SG-21 — Compliance tracking does not work (P1)

Beyond the tenant leak, the module is functionally empty: KPI cards read Valid 0 / Expiring soon 0 / Expired 0, and all 39 documents render as **UNKNOWN** with *Expires —*. No document has an expiry date, so nothing can expire, so the compliance engine can never fire. Meanwhile the dashboard raises a document-expiry alert for 1 vehicle. For a fleet product sold to operators who must prove insurance, registration and licence validity, this is the module that closes deals — and it is a shell.

### SG-25 — Maintenance contradicts itself (P2)

Hard-pinned to Sheikh Travel regardless of context, with no switcher. Total vehicles 2, Due for service 0, Under maintenance 0, Overdue 0 — yet **Active work orders 8** and **Pending requests 10**, and Monthly cost **PKR 0**. Eight open work orders against zero vehicles under maintenance cannot both be true. The only visible work order, #WO-00009, was due 3 July 2026 and is CLOSED.

### SG-26 — Wrong currency symbol (P2)

The *Highest combined cost vehicles* widget prints **$0.00** for both vehicles while the entire rest of the application uses PKR. A hard-coded `$` in that component.

### SG-45 — Inspections is a stub (P2)

One record: Toyota Crolla, inspected by Imran Yousaf on 20 July 2026, odometer blank, result **FAIL**. Four months later the FAIL is still open, with no status workflow, no KPI cards, no filters and no pagination. A failed safety inspection that nothing escalates is a liability, not a feature.

### Fuel — time windows are wrong (part of SG-15)

The module is built on a single fill-up dated 20 July 2026, yet presents it as *10.0 L this week*, *AVG / DAY 10.0 L*, *vs prior week* and *Fleet fuel spend stable*. A four-month-old record is being reported as this week's consumption.

### Electric fleet

0 EV vehicles, 0 range alerts, 2 seeded charging stations in Lahore. The module is shipped and visible in the nav with nothing behind it — fine as a roadmap item, but it should not be enabled for tenants who cannot use it.

## Fleet Operations — bookings, trips, routes

### SG-17 — Trip and booking statuses drift apart (P1)

Trip **TR-2026-2003** is marked *Cancelled*. The booking it is linked to, **BK-2026-3011**, is marked *Completed* and carries PKR 7,000. Nothing reconciles the two states, so a cancelled trip is still billable and still counted in completed-booking revenue. This is a state-machine problem, not a display problem: booking status and trip status need one owner and one transition path.

### SG-30 — Trip KPI row does not add up (P2)

*Total trips 6* against *Scheduled 1 + In progress 0 + Completed 4 + Delayed 0 = 5*. There is no Cancelled counter, so the cancelled trip falls out of every bucket. Either add the counter or exclude cancelled from the total — currently a user cannot tell which trip is unaccounted for.

### Bookings

Five bookings, all *Completed*, nothing pending, confirmed, started or cancelled. Two observations worth acting on:

- Amounts PKR 15,604.35 and PKR 12,868.33 carry fractional paisa on a cash-based booking flow — these look computed rather than quoted. Decide on a rounding policy at quote time.
- Booking numbers run BK-2026-1010, 1011, 2010, 3010, 3011 — three separate series with gaps. If the series is per-branch or per-year it should be documented; if it is not, the gaps will be queried by any auditor.

### SG-41 — Route pricing has no internal logic (P2)

| Route | Distance | Base price | PKR / km |
| --- | --- | --- | --- |
| Islamabad – Murree | 65 km | 2,500 | 38.5 |
| Karachi – Hyderabad | 165 km | 3,500 | 21.2 |
| Islamabad – Peshawar | 190 km | 4,000 | 21.1 |
| Pasrur – Chiniot | 300 km | 2,000 | 6.7 |
| Lahore – Multan | 340 km | 6,000 | 17.6 |
| Lahore – Murree | 437.9 km | 10,800 | 24.7 |
| Lahore – Karachi Overnight | 1,200 km | 18,000 | 15.0 |

Pasrur–Chiniot is priced at a third of the per-km rate of every other route, and is cheaper in absolute terms than a 65 km trip. Then booking BK-2026-3011 on that route was charged **PKR 7,000** — 3.5× the base price — with no surcharge, waiting-time or multiplier visible anywhere in the record. There is no audit path from base price to invoiced price.

Separately, route prices render without a currency symbol (`2,000`) while the rest of the app writes `PKR 2,000`.

### Assignments

16 total, 2 active, 0 available vehicles, 0 available drivers, utilization 100%. Assignment ASN-005012 shows duration *11d* with an End date of — and status ACTIVE, so "11d" is elapsed time on an open-ended assignment rather than a planned duration; the column should say so. The **Created by** column prints the raw user id `1` instead of a name.

### SG-23 — Driver allowance rules (P2)

Five rules, and all five show **Type: Unknown** — the enum is not mapping between API and UI, so an operator cannot tell a fixed allowance from a per-km or profit-share rule without opening each one. Two rules share the name *Short Trip Fixed Allowance*: priority 1 covers ≤ 50 km and priority 100 covers 8–50 km. Since the engine takes the first match in ascending priority, rule 100 can never fire — it is dead configuration that looks live. The paginator under the table reads **0 of 0** while five rows are rendered.

## GPS and tracking

The plumbing works: Traccar v6.14.5 reports *Server online*, the ERP polls every 5 s, Traccar sync last ran 6 seconds ago, 2 devices registered in both ERP and Traccar, and a SignalR hub negotiates successfully on every page load. The problem is what the UI concludes from that data.

### SG-04 — "Online" does not mean online (P0)

/gps-tracking/devices, the only row it renders:

| Field | Value |
| --- | --- |
| Vehicle / plate | Ali · BWY 217 |
| Driver | — (unassigned) |
| Tracker | JIMI IOT VG03, IMEI 862292058472119 |
| Status | *Last known: Online* |
| Ignition | ON *(cached)* |
| Speed | 15 km/h *(cached)* |
| Last seen | **5h ago** |
| Battery | *Cached* · Ext. Power |

Every live value is a cached value from five hours ago, and the device still counts as Online. GPS Control Center states that health KPIs use a 30-minute last-seen window — a device last seen 5 hours ago fails that window by a factor of ten, yet appears in the Online bucket on four screens (Devices, Live Map, Dashboard, Vehicles).

The downstream effect is the worst part: /vehicles shows this asset as **ON ROUTE** at 15 km/h with a live-looking coordinate. A customer watching their shipment would see a moving truck that has not reported since this morning. For a product whose headline is GPS reliability, the fix is to define three states — Live (< 5 min), Stale (5 min – window), Offline (> window) — and never render a cached value without a visible staleness badge.

### SG-12 — The same fleet is counted four ways (P1)

| Screen | Total | Online | Moving | Idle | Parked |
| --- | --- | --- | --- | --- | --- |
| Dashboard | 2 | 2 | 0 | 1 | 1 |
| GPS Live Map | 2 | 2 | 1 | 0 | 1 |
| GPS Devices | 2 | 2 | 0 | 1 | 1 |
| GPS Control Center | — | **4** | — | — | — |
| Companies (platform) | **4** active vehicles | — | — | — | — |
| Vehicles (Hannan Khokhar) | **1** | 1 | — | — | — |

Live Map disagrees with Dashboard and Devices on motion state at the same instant; Control Center and the Companies page count across tenants while the others do not. There is no single source of truth for "how many vehicles are there and what are they doing".

### SG-24 — Devices list drops a row (P2)

The footer reads *Showing 1–1 of 1 (2 total)* with one row rendered. Two devices are registered; one never appears in the table and cannot be opened, commanded or unassigned from the UI.

### SG-32 — Nobody is working the alert queue (P2)

/gps-tracking/alerts: **1,795 unread** alerts, 12 today, 0 critical open, 0 SOS open. Thirteen alert types are configured (SOS, speed, geofence, power cut, fuel fraud, fuel-station stop, and so on). An alert backlog of 1,795 with zero acknowledgements means the alerting feature is generating noise, not action. Either the severity thresholds are too loose or there is no acknowledgement workflow anyone is expected to run.

### SG-53 — History replay KPIs do not reconcile (P1)

GPS history audit, 26 Nov 2026 (`/gps-tracking/history`, vehicle 50, Last 7 Days). The KPI strip contradicted itself:

- NON-MOVING was computed against the filter window, which ends at 23:59 tonight. That counted hours that had not happened yet.
- STOPS and PARKING read 0 while the same strip showed six days standing.
- AVG SPEED was a mean of point speeds, not DISTANCE ÷ MOVING.
- ENGINE HOURS was moving time.

Fix:

- The replay window is clamped to now, so NON-MOVING = min(to, now) − from − moving.
- Stops are detected from the route when Traccar's stops report comes back empty.
- AVG SPEED = DISTANCE ÷ MOVING.
- ENGINE HOURS = ignition-on time, or Traccar engine hours, or "—" when there is no ignition data.
- "Last 7 Days" is fully rolling.

The five-hour gap between live position and replay end is the tracker clock skew (`fix/gps-tracker-clock-skew`), not ingestion lag. Replay reads Traccar directly.

### SG-55 — Tracker IMEI shown unmasked on the history vehicle card (P1)

The vehicle card on `/gps-tracking/history` showed the full IMEI beside the driver name, plate and live position to every user with GPS access.

Fix: the card shows the last four digits (`•••• 0598`) unless the user holds `Gps.DeviceManage`. The API payload still carries the IMEI; masking it server-side is a follow-up, because the vehicle edit form needs the real value.

### SG-56 — GPS history screen UX (P2)

Same audit, findings 11–16:

- The loading bar showed a timer-driven percentage that stalled at 92% and named the vendor.
- The details panel overlaid the map and hid the 0.5×/1×/2× speed buttons.
- "No vehicles match your search" appeared with an empty search box.
- "1 selected for compare" offered a mode that does nothing with one vehicle.
- "Hide records before" only filtered the stops list.
- A persistent alert toggle sat in the filter bar, styled as an error.

Fix: indeterminate bar; the panel docks beside the map; correct empty states; the compare chip needs two vehicles; the date control is "Hide stops before"; the toggle moved to Alerts → Rules. The stationary alert is still evaluated in the browser only — a server-side rule is a follow-up.

### SG-57 — GPS history accessibility and polish (P3)

Same audit, findings 18–19: KPI labels at 2.43:1 contrast; icon buttons with no accessible name; a second `<main>` inside the shell's; wrapping speed buttons; the map zoom control under the AI assistant button; plates saved in mixed formats (`LEC-8825`, `BWY 217`).

Fix: labels `#4b5563` at 11 px; `aria-label`s; one `<main>`; no-wrap speed group; zoom control moved; plates normalised at entry (upper-case, single hyphen). Outside the repo: the vector Map ID (Google Cloud) and battery/GSM on every fix (Traccar `processing.copyAttributes`).

### Device command failures

GPS Control Center: 10 manufacturers, 27 models, 22 commands, 107 templates, **2 queued and 6 failed** commands. Six failed device commands sit with no retry policy and no alert; they are only discoverable by opening this page. It also warns that weak-signal and low-battery KPIs are placeholders "until richer telemetry arrives".

### Driver performance and client links

Driver Performance exposes configurable score weights (safety / fuel / on-time) but all three fields render empty, with no default shown — so the composite score (Munna Driver: 51/100) is computed from weights the operator cannot see. The page notes "Changes are audited", which is not currently true given SG-07.

Client Tracking Links is fully built — tokenised, expiring, revocable — and completely unused: 0 active, 0 views, 0 expired, 0 revoked. This is the most sellable feature in the GPS module and no tenant has been onboarded onto it.

## Finance

This is the weakest area of the platform and the one that would fail an accounting review first.

### SG-02 — Revenue collected, zero invoices issued (P0)

/invoices: *Total billed 0.00 · Collected 0.00 · Overdue 0 · **No invoices found***.

Against that: 5 completed bookings worth PKR 43,142 and 9 payment records worth PKR 39,824. Money has moved and no invoice exists for any of it. There is no accounts receivable, no tax document, no numbered billing series, nothing to send a corporate client, and nothing to hand an auditor. The module is described as "trip-linked client billing with email delivery" and has never produced a document.

### SG-03 — Payments are orphaned (P0)

| Amount | Method | Status | Paid at | Booking |
| --- | --- | --- | --- | --- |
| PKR 200 | JazzCash | PartiallyPaid | 20 Jul 2026 | *(empty)* |
| PKR 500 | Cash | PartiallyPaid | 20 Jul 2026 | *(empty)* |
| PKR 11,974 | BankTransfer | PartiallyPaid | 8 May 2026 | *(empty)* |
| PKR 11,000 | BankTransfer | PartiallyPaid | 8 May 2026 | *(empty)* |
| PKR 3,750 | Card | PartiallyPaid | 7 May 2026 | *(empty)* |
| PKR 0 | Card | Pending | 7 May 2026 | *(empty)* |
| PKR 4,200 | Cash | Paid | 7 May 2026 | *(empty)* |
| PKR 2,000 | Card | PartiallyPaid | 7 May 2026 | *(empty)* |
| PKR 6,200 | Card | Refunded | 7 May 2026 | *(empty)* |

The Booking column is empty on every row, so no payment can be traced to a trip, a customer or a vehicle. Three further problems are visible in the same table:

- **PartiallyPaid is being stored on the payment.** Partial settlement is a property of the invoice or booking balance, not of an individual receipt. Seven of nine rows carry it, which makes the ledger unreadable.
- **A PKR 0 Pending payment** exists — a record with no amount and no linkage.
- **A PKR 6,200 refund** sits in the same list as receipts, with no netting anywhere; the page has no totals row and no summary card, so the PKR 39,824 above had to be added by hand.

No payment has been recorded since 20 July 2026.

### SG-14 — Revenue recognition ignores partial payments (P1)

The dashboard's Total revenue (PKR 4,200) equals exactly the single payment with status *Paid*. PKR 35,624 of PartiallyPaid receipts is excluded from every revenue figure in the product, including /reports. For the current period /reports shows Total revenue PKR 0, Fuel PKR 0, Maintenance PKR 0, Net profit PKR 0 — four empty cards where management reporting should be.

### SG-44 — Driver settlements never run (P2)

/settlements: *No settlements yet.* Five allowance rules are configured, trips have completed, and not one settlement has been generated or approved. Drivers are, in system terms, unpaid. The generator requires a driver, period start and period end to be chosen manually — there is no scheduled run, so settlements only exist if someone remembers.

### Why these four are one problem

Booking → trip → invoice → payment → settlement is a single chain, and three of the four links are missing. Fixing the dashboard KPIs without first making invoices exist and linking payments to bookings will just produce prettier wrong numbers. The order has to be: invoice generation on trip completion → payment-to-invoice linkage → balance-driven status → then rebuild revenue, AR and net-profit cards off the ledger.

## Customers

Seven customers, all Active, all created between 8 and 15 May 2026. *New (30 d): 0*. No customer has been added in six months.

### SG-18 — CNIC numbers displayed in the list view (P1)

Every customer row prints a full 13-digit CNIC in plain text, visible to anyone who can open /customers, exportable in one click via the Export button, with no masking and no separate permission. Under Pakistan's data-protection expectations — and under any enterprise client's vendor review — a national identity number is restricted data. It should be masked to the last four digits in list views, require an explicit `Customer.ViewPII` permission to reveal, be excluded from the default CSV export, and have every reveal written to the audit log. (The audit log problem in SG-07 makes the last part impossible today.)

The same applies to the customer phone numbers, which are also unmasked, though those are lower risk.

### SG-35 — Seed data and real data are mixed in production (P2)

Five of the seven customers are obvious fixtures — `ahmed.khan@example.com`, `bilal.raza@example.com`, `sara.malik@example.com`, `hina.ali@example.com`, `usman.sheikh@example.com`, each with sequential phone numbers (0300 1111111, 2222222, 3333333…) and sequential CNICs. Two look real (Hamza, zirwa) and both have quality problems: Hamza's address is `OJIOJOI`, and his phone is stored as `92308387129` while every other record uses the `03xx` format.

This pattern repeats across the platform: a driver named "gg gg" in compliance documents, a department called "oprations 5717", a vehicle named "Ali", a workshop named "Ali Workshop". Before any demo to a real prospect, production needs a clean-data pass — and ideally a separate seeded demo tenant so fixtures never land in the live database again.

### Missing from the module

For a transportation ERP the customer record is thin: no company/organisation field (so corporate clients cannot be distinguished from individuals), no credit terms or credit limit, no linked bookings or payment history on the record, no outstanding balance. Two of seven customers have no email at all, which blocks the invoice-by-email feature that /invoices advertises.

## Access control, security and audit

### SG-05 — The SUPER\_ADMIN role grants nothing (P0)

Access Control → Roles:

| Role | Category | Visibility | Users | Permissions |
| --- | --- | --- | --- | --- |
| Tenant Admin (TENANT\_ADMIN) | Platform | SYSTEM | 1 | 81 |
| Fleet Manager | Fleet | SYSTEM | 0 | 45 |
| Owner | Fleet | SYSTEM | 0 | 34 |
| Dispatcher | Operations | SYSTEM | 0 | 18 |
| Driver Manager | Fleet | SYSTEM | 0 | 16 |
| GPS Operator | Fleet | SYSTEM | 0 | 13 |
| Accountant | Finance | SYSTEM | 0 | 11 |
| Driver | Fleet | SYSTEM | 0 | 8 |
| **Super Admin (SUPER\_ADMIN)** | Platform | SYSTEM | **0** | **0** |

The role with the most power in the system has an empty permission set and no users attached, while the Data Scope tab reports *My effective scope: Company · source **super\_admin*** and the Permissions tab reports *My effective permissions: 119*. Access is therefore granted by a hard-coded check on the role name somewhere in the authorisation pipeline, not by the RBAC tables the UI presents.

Three consequences. You cannot answer "what can the super admin do?" from the data. You cannot grant a narrower platform-operator role (support, billing, read-only auditor) because there is no permission set to subtract from. And if that bypass is ever refactored or a code path checks the table instead of the name, the super admin silently loses or gains access.

Note also that the super admin's effective scope is *Company-wide **within this tenant*** — the data layer has no concept of a platform-wide scope. That is the formal statement of the visibility gap in the section above.

### SG-08 — Security Center shows controls that do not enforce (P0)

| Policy | Value | What the UI itself says |
| --- | --- | --- |
| MFA required | false | *"Flag only in foundation — no MFA challenge"* |
| Trusted devices only | false | *"Future metadata only. No device enrollment in foundation"* |
| Password history count | 0 | *"Reserved for future history checks. Stored only in foundation"* |
| Restrict by IP | false | *"Soft-check … soft allowlist only — not a firewall"* |
| Data retention (days) | 365 | *"Advisory retention hint for operators"* |
| Password complexity | **false** | Enforced, but turned off |
| Minimum password length | 8 | Enforced |
| Password max age | 0 (disabled) | Enforced |
| Max failed attempts / lockout | 5 / 15 min | Enforced — the one control doing real work |
| Idle / absolute session timeout | 30 min / 480 min | Enforced |
| Audit level / login events | Always / true | Enforced in config, but see SG-07 |

Five of eleven controls are display-only. **Last updated is `—` on every single policy**, meaning no security setting has ever been changed from its default on this platform. The practical position today: no MFA, no password complexity, no password expiry, no device trust, no IP restriction. A single leaked password is full access to a tenant — and, through the super-admin bypass, potentially to the platform.

The security-review risk is sharper than the technical one. An enterprise or government buyer who opens this page sees a mature-looking policy console; the first question they ask is "is MFA enforced?" and the honest answer is no.

### SG-07 — There is no usable audit trail (P0)

/platform/audit-center and /audit-logs render the same component and both return **zero rows**, blocked by the message *"Select a company from the platform tenant switcher"* — the switcher that does not exist. The page advertises itself as an *"immutable activity trail for compliance review"* with 16 categories and severity/outcome filters, and audit level is set to *Always*.

So: the system is probably writing audit events and nobody can read any of them. There is also no "all companies" option, so even with a working switcher a super admin could never see a platform-wide trail — which is exactly what an audit trail is for. Several features ("Changes are audited" on driver score weights, GDPR logging = true) make promises this backs.

### SG-52 — Cross-tenant read / subscription audit is fail-loud, not fail-closed (P1)

Cross-tenant read and subscription change audit writes throw at the API boundary but are not in the same transaction as the action, so the action can commit without an audit row. Labelled **fail-loud**, not fail-closed.

True fail-closed remains only where the audit INSERT shares the SQL transaction (permission change, role assignment).

Future fixes (do not implement in the SG-07 pass):

- **Reads:** write + commit the audit row first, then perform the read (only practical way to fail-closed reads).
- **Subscription:** can move into the same SQL transaction as the subscription mutate.

### SG-16 — Menu visibility gated on the wrong permission (P1)

Menu Management shows the **Vehicles** item at /vehicles guarded by `Vehicle.Create`. Every sibling uses a View permission (`Driver.View`, `Maintenance.View`, `Fuel.View`, `GPS.View`). Any read-only role — Accountant, Driver, GPS Operator, a future auditor role — loses the Vehicles menu entirely despite being able to open the page by URL. One-line fix, but it is the kind of thing that makes a role test fail in UAT.

### SG-28 — Role templates have drifted from the roles (P2)

The Role Templates tab offers *Company Admin (TENANT\_ADMIN) — 98 permissions*. The Roles tab shows the live TENANT\_ADMIN role with **81**. Applying the template would silently add 17 permissions to every tenant admin. Templates and roles need to be versioned against each other, and applying one should show a diff before it commits.

### SG-22 — MCP console exposed inside the production ERP (P1)

/mcp presents *SheikhGo MCP Console — 14 servers, 12 running, 117 registered tools*, with a **Connect Client** action for Cursor and Claude and a built-in chat. It is reachable from the Administration menu by anyone whose role shows it. Combined with no MFA and a role-name-based super-admin bypass, this is a developer-grade automation surface sitting in the same app as customer PII and GPS history. It should be environment-gated (non-production only) or, at minimum, behind its own permission plus MFA, with every tool invocation audited.

### SG-29 — Migrations auto-apply on startup (P2)

Migration Manager: 171 applied, 0 pending, all applied **18 Jul 2026 12:04 by "Startup"**. Schema changes running automatically when the app boots is convenient in development and dangerous in production — a bad deploy mutates the schema before anyone can stop it, and rollback is manual. Move to an explicit, gated migration step in the deployment pipeline. The *Apply Pending* button here is the right idea; it should be the only path.

**Priority note (after two startup-migration incidents):** Do **not** leave SG-29 for a month. Take it **right after SG-01**. Evidence: SG-05 permission sync on Startup risked lockout if a bad seed shipped; SG-07’s first Startup attempt half-applied schema (column/triggers before `SchemaMigrationHistory`) because the runner has no per-migration SQL transaction and the menu UPDATE was non-idempotent. Scope when scheduled: **remove auto-migrate from startup** → **explicit gated deploy step** only. Do not implement in the same pass as SG-01.

## Platform and Organization

### SG-06 — Three screens are unusable without a switcher that does not exist (P0)

Security Center, Audit Center and Workspace Management all end with *"Select a company from the platform tenant switcher"*. The application header contains only global search, a clock, help, notifications and a profile menu (Profile / Settings / Logout). There is no tenant switcher anywhere in the chrome. These three screens cannot be used at all — Security Center shows its policy tables but cannot bind them to a company; Audit Center shows nothing.

The fastest fix is the one the product already half-implements elsewhere: promote the *Select Tenant* dropdown that Access Control and Module Registry use into the global header, make it the single source of tenant context for every module, and add an **All companies** option that the list endpoints honour.

### Organization structure is pinned to one tenant

/platform/branches and /platform/departments both say *"for the current tenant"* and carry no selector, while /platform/organization-designer has its own *Select company* control. So the org tree can be viewed for any company but its branches and departments can only be edited for one.

Branches (2): `HQ-001 Head Office — Karachi` with no type and no manager, and `787H Pasrur Branch — LAHORE` (uppercase, while the other is title case). Branch codes follow two different conventions.

Departments (5): finance 1, Fleet, HR, Operations, **oprations 5717**. A typo duplicate of Operations with a random numeric suffix is live in production. Member counts: Fleet 2, everything else 0 — against 9 platform users, so departments are essentially unused and the Branch/Department columns are blank on every user and driver record.

### SG-27 — Platform Settings is entirely unconfigured (P2)

/settings → General: Company Name blank, Logo URL blank, Address blank, Phone blank, Email blank, Website blank. Time Zone, Currency, Language, Date format and Time format all sit at *Select…*. Thirteen settings sections exist (Tenant, Localization, Security, Notifications, Retention, Documents, Workflows, Numbering, File Management, Contact & Locale, System Preferences, Integrations, Audit & Logging, Feature Management, AI Settings) and the foundational one has never been filled in.

This is why PKR appears hard-coded in some components and `$` in others (SG-26): there is no configured default currency for anything to read. Same for the timezone — the header shows Karachi GMT+5 while the tenant list is all-Pakistan but the currency picker offers AED/SAR, implying a Gulf go-to-market. Set these before the first non-Pakistani tenant onboards.

### Module Registry and Subscriptions

The Module Registry is genuinely good: 39 modules, 14 installed, 13 coming soon, per-company toggles, dependency declarations (`Depends: GPS`, `Depends: ALERTS`), and licence/mobile/GPS flags. It is the most mature part of the platform console.

Subscription Management for Hannan Khokhar Travels shows the commercial model clearly — Starter, PKR 20k per GPS device per year, 50 licensed devices, PKR 1.0M yearly — but two things need attention:

- A **"Monthly Amount (legacy)" of PKR 83k** sits beside the yearly figure with Billing Cycle = Monthly. Two pricing models coexist and the page does not say which one bills.
- **Auto Renewal is Off**, the plan expires 12 Dec 2026, and the warning *"This subscription expires within 30 days"* is shown only on this page. Sheikh Travel's plan has already **Expired** and its 5 users still have access — there is no enforcement linking subscription state to login.

### Menu and workspace catalogs

Menu Management is a real admin feature (labels, routes, permissions, visibility per item) with housekeeping debt: the *Operations* module is Visible with 0 items; a dead entry *Vehicle List* points at `/VehicleList` in PascalCase while every other route is kebab-case; the *Fleet Tracking* item points at `/gps-tracking` while the sidebar links `/gps-tracking/live`; the *Administration* module declares 7 items but only 3 render in the sidebar; and module slugs mix conventions (`access_control`, `vehicle-rental`, `travel-fleet-operations`, `fleet`).

Dashboard Management (6 catalog dashboards across ERP and Mobile) and Workspace Management (8 landing workspaces) are well-structured; Workspace Management is one of the three blocked by the missing switcher.

### Database Reset

Correctly gated: *"Database reset is only available in Development and Staging environments for Super Admin users"*, and it refuses to run here. This is the one destructive operation in the platform and it is handled properly.

## Vehicle Rental

### SG-33 — The module is live with no rates configured (P2)

/rental/rates: *No rates configured.* Zero hourly, daily, weekly or monthly tariffs exist — neither fleet-wide defaults nor per-vehicle. /rental/availability and the bookings list are empty too.

Meanwhile the *Create rental* form is fully enabled and will let an operator pick a customer, a vehicle, a rate type and a date range and click **Create draft** with no price behind it. Either the form should block on "no rate configured for this vehicle and rate type", or the module should not be enabled for a tenant until at least one default rate exists.

The rate form itself is well designed — vehicle-optional for fleet-wide defaults, a deposit field, a currency field, and the correct note that *"confirmed bookings keep their rate snapshot — changes here apply to new quotes only"*. That snapshot behaviour is the right call and worth keeping.

### Scope confusion

The customer dropdown lists all 7 customers and the vehicle dropdown lists *Ali (BWY 217)* and *Toyota Crolla (LEC-8825)* — Sheikh Travel's assets — while /vehicles was in Hannan Khokhar Travels context at the time. Another instance of the four-switcher problem: rental is pinned to a different tenant than the fleet module.

### Gaps for a real rental business

No security deposit workflow beyond the field, no damage or condition report at pickup and return, no mileage cap or excess-km charge, no late-return penalty rule, and no link from a rental to an invoice (which cannot exist anyway, per SG-02). If rental is meant to be a revenue line rather than a checkbox on the feature list, these are the next things to build.

## WhatsApp

### SG-19 — The Meta webhook is rejecting every payload (P1)

/whatsapp/webhook-logs holds exactly one record:

| Id | Received | Sig | Status | Attempts | Error |
| --- | --- | --- | --- | --- | --- |
| 1 | 23 Oct 2026, 6:38 AM | **Bad** | **Rejected** | 0 | `Invalid signature` |

One inbound payload in the platform's history, and it failed signature verification. The usual causes, in order of likelihood: the app secret configured in SheikhGo does not match the one in the Meta app; the signature is being computed over the parsed JSON body instead of the raw request bytes (the classic ASP.NET Core mistake — you need `EnableBuffering()` and the raw body for the HMAC-SHA256 against `X-Hub-Signature-256`); or a proxy is re-serialising the body before it reaches the handler.

Until this is fixed, inbound WhatsApp is dead. That matches the Notification Center, which reports **WhatsApp: 0** across 3,101 notifications while Email, SMS, Push and Browser all have traffic — so outbound WhatsApp is not working either.

Attempts = 0 on a rejected payload also means nothing retried it, and the *Requeue* button is the only recovery path. A rejected-signature payload should alert someone, not sit in a log.

### Inbox

One conversation: *Ali sheikh, +971 55 770 1219, DEMO PK*, dated 25 September 2026, **Unassigned**, Bot, Open. Seven weeks unanswered, no owner. The inbox has the right primitives — assignment, bot/human handoff, Open/Pending/Resolved states, per-account filtering — and no one using them. If WhatsApp is meant to be a lead channel for SheikhGo, an unassigned demo enquiry sitting open for seven weeks is a lost deal.

### Information architecture

WhatsApp is a top-level sidebar group but its breadcrumb reads *Administration › WhatsApp › Inbox*. Templates, Automations, Accounts and Webhook Logs are all present and configured; the module is built, it is the integration and the operational habit that are missing.

## Website CMS

The CMS itself is sound: 5 pages (Home, About, Features, Fleet Management, GPS Tracking), all published, 9 feature entries, a legal section with Privacy Policy v1.0 drafted and published, and a media library. It is the commercial result that is the finding.

### SG-40 — Zero inbound leads, and nothing has shipped since September (P3, but commercially urgent)

| Metric | Value |
| --- | --- |
| Contact requests | **0** (0 new) |
| Demo requests | **0** (0 new) |
| Media library | **0** items |
| Last published | 18 Sep 2026 |

Two months of a live marketing site with both lead forms completely empty. There are two possible explanations and they need different fixes:

1. **The forms are broken.** Given SG-19 (webhook signature failure) and SG-10 (1,748 failed notifications), a silently failing POST is plausible. Test both forms end to end from an incognito browser and confirm a row lands in /website/contact-requests.
2. **The site gets no qualified traffic.** Then the fix is marketing, not engineering — and the lead pipeline (New → Contacted → InProgress → Qualified → Converted → Closed) that is already built here is ready for it.

Either way, verify (1) first — it costs ten minutes and rules out the expensive possibility.

### Media library is empty

Zero assets, on a site selling a visual product (live maps, dashboards, a driver app). The pages are presumably using hard-coded images from the build rather than CMS-managed media, which means marketing cannot change a screenshot without a developer and a deploy.

### Legal

Privacy Policy v1.0 is published with real content. Given what the platform processes — continuous GPS location history, driver behaviour scoring, and CNIC numbers — the policy should be reviewed specifically for: location-data retention (Security Center advertises 365 days as "advisory" only), who the data controller is in a multi-tenant arrangement (SheikhGo vs the fleet operator), and sub-processor disclosure for Traccar and Meta/WhatsApp. Terms and Cookie tabs exist; confirm both carry current content before any enterprise procurement review.

## Analytics, Reports and AI

### Reports

/reports offers five report families — Revenue & Bookings, Vehicle Profit, Driver Performance, Payment Report, Fleet Reports — with period presets and CSV export. For 1–16 November 2026 all four headline cards read **PKR 0**: revenue, fuel expense, maintenance, net profit.

That is arithmetically consistent with no activity since July, but it means management reporting is blank in the current period and will stay blank until the finance chain (SG-02, SG-03, SG-14) is fixed, because revenue is read from fully-paid payments rather than from invoices. There is also no company selector here, so a super admin cannot produce a cross-tenant report — which is the report a platform owner most needs.

### SG-10 — 56% of notifications fail (P1)

|  | Count |
| --- | --- |
| Total notifications | 3,101 |
| Unread | 1,608 |
| Read | 1,493 |
| **Failed** | **1,748** |
| Email / SMS / Push / Browser / WhatsApp | 588 / 363 / 472 / 508 / 0 |

Two separate problems in one table. First, 1,748 failures out of 3,101 is a broken delivery pipeline, not a glitch — and the UI exposes the count without a reason code, so the cause has to be found in server logs. Second, **the channel counts sum to 1,931, not 3,101**; 1,170 notifications belong to no channel in the breakdown.

The content is also stale: the newest items are maintenance reminders for *"Vehicle #4 maintenance due on 2026-08-08"*, still being sent on 16 November. A reminder for a date three months past is re-firing because nothing closes the loop when the underlying task is never actioned. Combined with 1,795 unread GPS alerts (SG-32) and 14 unacknowledged operations alerts, the platform produces roughly 5,000 unread signals that no human reads — which is the same as having no alerting at all.

### AI Assistant

/ai is well-executed: *Connected to Live Fleet Data*, SheikhGo tool calling, suggested prompts (vehicles offline, today's trips, vehicle health, pending bookings, fuel report), plus Recommendations, Predictions and Escalation tabs. It reports Online.

The risk is upstream, not in the AI: an assistant that answers "how many vehicles are offline?" from the same data that calls a 5-hour-stale device *Online* (SG-04) will confidently give wrong answers, and users trust a chat answer more than a dashboard card. Fix the staleness definition before promoting the assistant.

### MCP Console

/mcp — 14 servers (12 running, 2 planned), 117 registered tools, v0.19.0, with servers for Analytics, Architecture and more, plus Connect Client for Cursor and Claude. As engineering tooling this is impressive. As a surface inside the production ERP it is the single largest unreviewed attack surface in the platform (see SG-22) and should be environment-gated.

## Cross-cutting issues

### What is working well

Worth saying first, because it changes how the rest should be read. Across 45 pages there were **zero JavaScript console errors and zero failed API calls** — every request returned 200. SignalR negotiates on every load. Traccar v6.14.5 is connected and syncing. All 171 schema migrations are applied with none pending. Navigation, breadcrumbs, filters, exports, empty states and loading behaviour are consistent and professional. The Module Registry, Menu Management and Dashboard Management consoles are genuinely well-built platform tooling.

The engineering is not the problem. The problems are scoping, data integrity and controls that are displayed but not enforced.

### SG-09 — Production API runs on a raw IP via sslip.io (P1)

Every call from the web app goes to `https://20-174-1-230.sslip.io/api/...`. sslip.io is a free third-party wildcard-DNS service that resolves an IP embedded in the hostname. Consequences:

- **The server's public IP is pinned into every client build** — web, and presumably the Flutter driver app. Change or lose that Azure IP and every client breaks at once, with no DNS-level failover.
- **No owned domain, so no CDN, WAF or DDoS protection** can sit in front of the API, and no certificate pinning strategy is possible.
- **A third party controls resolution** of your production API hostname.
- **It fails enterprise and government procurement on sight.** No buyer's security team passes an API endpoint that is an IP address on a free DNS service.

Fix: `api.sheikhgo.com`, certificate via your own CA chain, CDN/WAF in front, and the base URL moved to runtime configuration so it is not baked into client builds.

**Progress, 26 Nov 2026.** The ERP now reads the API host at startup from `assets/config.json` (`Frontend/sheikhgo-erp/src/config/`), and no host is baked into its bundle. `api.sheikhgo.com` has no DNS record yet, so the production config still points at the sslip.io host. Remaining: add the A record, change one line of JSON, then the CDN/WAF. The Flutter apps still carry the host as a build default.

### SG-54 — Session tokens in localStorage (P1)

`access_token`, `refresh_token` and `current_user` sat in `localStorage` in clear text, and the origin carried no cookies. Any XSS anywhere in the app, including through a third-party script, could read a full session plus the refresh token that renews it.

Fix: the API can set the tokens as `HttpOnly; Secure; SameSite=Strict` cookies (`useCookies` on login and refresh) with a CSRF header check on cookie-authenticated writes. The ERP uses that mode when `authMode` is `cookie` and the API is same-site with the page. Bearer tokens are unchanged for the mobile apps. Development runs in cookie mode. **Production stays on bearer until the API is on `api.sheikhgo.com`** (see SG-09), because browsers do not send these cookies cross-site.

### SG-34 — Duplicate and over-fetching API calls (P2)

One dashboard load issues 18 requests, including `/api/smart-alerts?limit=50` **twice** and `/api/platform/menus/me` **twice**. It also pulls `/api/bookings?page=1&pageSize=50` and `/api/notifications?page=1&pageSize=50` — 50 full records each — to render a 5-row table and a counter. In Angular this is the classic missing `shareReplay(1)` on a shared service observable plus a component subscribing in both `ngOnInit` and a template `async` pipe. At two vehicles it costs nothing; at 500 vehicles across 50 tenants it is the first thing that falls over.

**Progress, 26 Nov 2026.** `/api/vehicles` (three calls on every GPS page), `/api/platform/menus/me` and `/api/smart-alerts` are down to one call each. Still open: the `pageSize=50` fetches, and the repeated fleet-status and fleet-health calls on the Live Map.

### SG-15 — Date filters are requested but not applied (P1)

The dashboard calls `/api/reports/bookings?fromDate=2026-10-17&toDate=2026-11-16` and then renders five bookings created in May–October under the heading *Bookings by status — Last 30 days*. The fuel module labels a 20 July fill-up as *this week*. Either the backend ignores the range parameters or the frontend falls back to an unfiltered list on an empty result. Both are worth checking; an unfiltered fallback that silently shows all-time data under a 30-day label is the more dangerous of the two.

### Data integrity patterns

The individual data bugs share three root causes worth fixing at the pattern level rather than one screen at a time:

1. **Enum mapping between API and UI is unreliable.** Allowance rule types all render *Unknown*; compliance document statuses all render *UNKNOWN*; company type renders blank for Chand Travels. Same failure mode, three modules. Audit every enum serialisation contract.
2. **KPI cards are computed per-component instead of from one source.** That is why utilization is 0% and 100% simultaneously, why trip counts do not sum, why channel counts do not sum to the total, and why four screens disagree on the vehicle count. Move aggregate KPIs into one query layer with defined windows.
3. **No cross-entity state machine.** Booking status, trip status, payment status and assignment status each change independently, which is how a *Cancelled* trip ends up under a *Completed* booking and a *Suspended* driver ends up on an active route.

### SG-35 — Test data in production

`gg gg` as a driver, `OJIOJOI` as an address, `oprations 5717` as a department, `Ali` as a vehicle, five `@example.com` customers with sequential phone numbers and CNICs, a `DEMO PK` WhatsApp conversation. Before the next prospect demo this needs a clean-up pass, and ideally a dedicated seeded demo tenant so fixtures never reach the live database again.

## Issue register

49 findings. P0 = fix before onboarding a paying company. P1 = this quarter. P2 = backlog with a date. P3 = housekeeping.

### P0 — blocking (8)

| ID | Module | Finding | Fix |
| --- | --- | --- | --- |
| SG-01 | Compliance | 39 documents from \~13 drivers of other companies shown under a tenant with 1 driver; no company selector | Add tenant predicate to compliance queries; audit every repository joining Drivers/Vehicles |
| SG-02 | Invoices | Zero invoices against PKR 39,824 collected and PKR 43,142 of completed bookings | Auto-generate invoice on trip completion; numbered series; backfill existing bookings |
| SG-03 | Payments | Booking column empty on all 9 payments — nothing traceable to a trip or customer | Make InvoiceId required on payment; backfill by date and amount |
| SG-04 | GPS | Device last seen 5h ago reported Online with cached ignition and speed; vehicle shown ON ROUTE | Three states (Live / Stale / Offline); never render a cached value without a staleness badge |
| SG-05 | Access Control | SUPER\_ADMIN role has 0 permissions and 0 users while effective permissions = 119 | Populate the role's permission set; remove the role-name bypass; add a read-only auditor role |
| SG-06 | Platform | Security Center, Audit Center, Workspace Management need a tenant switcher the header does not have | Promote Select Tenant into the global header with an All companies option |
| SG-07 | Audit | Audit Center and /audit-logs return 0 rows, blocked by the missing switcher, despite audit level Always | Unblock the page; add platform-wide view; verify events are actually being written |
| SG-08 | Security | MFA, trusted devices, password history and IP restriction are display-only; no policy ever changed | Implement TOTP MFA; enable password complexity; mark non-enforcing toggles clearly until built |

### P1 — fix this quarter (16)

| ID | Module | Finding | Fix |
| --- | --- | --- | --- |
| SG-09 | Infrastructure | Production API on `20-174-1-230.sslip.io` — raw IP, free third-party DNS, no WAF | Move to api.sheikhgo.com behind CDN/WAF; base URL from runtime config, not the build |
| SG-10 | Notifications | 1,748 of 3,101 notifications failed (56%); no reason code exposed | Surface failure reason and retry; alert on failure rate above threshold |
| SG-11 | Platform | Suspended companies (A Ali, Vision Factory) missing from the tenant selector | Include suspended tenants, flagged, so suspension is reversible from the console |
| SG-12 | GPS / Fleet | Vehicle counts differ across six screens (1 / 2 / 2 / 2 / 4 / 4) and motion states disagree | One aggregate KPI query layer with defined windows |
| SG-13 | Dashboard | Net profit PKR −24,330 subtracts \~PKR 25,400 maintenance that Maintenance reports as PKR 0; mixes three time windows | Rebuild from the ledger; label every card's window |
| SG-14 | Finance | Revenue counts only fully-Paid payments; PKR 35,624 of PartiallyPaid excluded everywhere | Recognise revenue from invoices, not payment status |
| SG-15 | Dashboard / Fuel | Date filters sent to the API but not reflected; July data labelled "this week" / "last 30 days" | Fix the range filter server-side; remove the unfiltered fallback |
| SG-16 | Access Control | Vehicles menu gated on `Vehicle.Create` while siblings use View permissions | Change to `Vehicle.View`; add a role-matrix test to CI |
| SG-17 | Operations | Trip TR-2026-2003 Cancelled under booking BK-2026-3011 Completed, still billed PKR 7,000 | One state machine owning booking and trip transitions |
| SG-18 | Customers | Full CNIC numbers displayed unmasked in the list view and in CSV export | Mask to last 4; gate reveal behind a PII permission; exclude from default export; audit each reveal |
| SG-19 | WhatsApp | Only inbound webhook payload rejected: `Invalid signature`; WhatsApp channel count is 0 | Verify app secret; compute HMAC over the raw request body, not parsed JSON |
| SG-20 | Drivers | Suspended driver attached to a vehicle currently ON ROUTE; counted as Active and Suspended | Block dispatch for suspended drivers; fix the contradictory KPI |
| SG-21 | Compliance | All 39 documents UNKNOWN with no expiry; KPIs read 0/0/0 while the dashboard raises an expiry alert | Make expiry mandatory on upload; backfill; drive the dashboard alert from the same source |
| SG-22 | Platform | MCP console (14 servers, 117 tools, Connect Client) exposed in the production ERP | Environment-gate to non-production, or require a dedicated permission plus MFA, with audited invocations |
| SG-42 | Users | /users shows 5 of 9 platform users; 4 invisible to the super admin | Cross-tenant user directory for platform roles |
| SG-50 | Billing / FX | Subscription pricing uses a hardcoded USD→PKR constant (280) in Backend `SubscriptionBilling:UsdToPkr` / `ToPkr` and ERP `USD_TO_PKR_DEFAULT`; no live rate, no drift alert | Fix with money chain SG-02/03/14 — live or Key Vault FX + drift alert; not a one-off patch |
| SG-52 | Audit | Cross-tenant read + subscription audit throws at API boundary but is not same-TX (action can commit without audit row); labelled fail-loud, not fail-closed | Reads: audit-first then read; subscription: shared TX later — do not pretend either is fail-closed today |
| SG-53 | GPS | History replay KPI strip does not reconcile: non-moving counts future hours, 0 stops beside 6 days standing, avg speed ≠ distance ÷ moving, engine hours = moving time | Clamp window to now; route-based stop fallback; derived avg speed; ignition-based engine hours |
| SG-54 | Security | `access_token` and `refresh_token` in localStorage — any XSS takes the session and the token that renews it | HttpOnly SameSite=Strict cookies for the ERP with a CSRF header check; bearer kept for mobile; production switch waits on SG-09 |
| SG-55 | GPS / Privacy | Full tracker IMEI on the history vehicle card for every user with GPS access | Show the last four digits unless the user holds `Gps.DeviceManage`; server-side masking later |

### P2 — backlog with a date (20)

| ID | Module | Finding |
| --- | --- | --- |
| SG-23 | Allowance rules | All 5 rules show Type "Unknown"; two rules share a name and priority 100 can never fire; paginator reads 0 of 0 |
| SG-24 | GPS Devices | Footer reads "1–1 of 1 (2 total)" — one registered device never renders and cannot be managed |
| SG-25 | Maintenance | 8 active work orders and 10 pending requests against 0 vehicles under maintenance; monthly cost PKR 0 |
| SG-26 | Maintenance | Cost widget prints `$0.00` while the whole app uses PKR |
| SG-27 | Settings | Company identity, contact, timezone, currency, language and date/time format all unset |
| SG-28 | Access Control | Role template Company Admin = 98 permissions vs live TENANT\_ADMIN = 81; applying it silently grants 17 |
| SG-29 | Platform | All 171 migrations applied automatically at app startup; no gated deployment step — elevate: do right after SG-01 (startup incidents SG-05/SG-07); remove auto-migrate → gated deploy only |
| SG-30 | Trips | Total 6 ≠ Scheduled 1 + In progress 0 + Completed 4 + Delayed 0; no Cancelled counter |
| SG-31 | Notifications | Channel counts sum to 1,931 against a total of 3,101 |
| SG-32 | GPS Alerts | 1,795 unread alerts, 14 unacknowledged operations alerts, zero acknowledgements ever |
| SG-33 | Rental | Module enabled with zero rates configured; the create form still allows a priceless draft |
| SG-34 | Performance | Duplicate API calls per page load; `pageSize=50` fetched to render 5 rows |
| SG-35 | Data quality | Seed and junk data in production: `gg gg`, `OJIOJOI`, `oprations 5717`, vehicle named `Ali`, five @example.com customers |
| SG-36 | Audit | `/audit-logs` and `/platform/audit-center` render the same component at two routes |
| SG-41 | Routes | Pasrur–Chiniot priced at PKR 6.7/km vs 15–38 elsewhere; a booking on it charged 3.5× base with no surcharge trail |
| SG-44 | Settlements | No settlement ever generated; no scheduled run — drivers unpaid in system terms |
| SG-45 | Inspections | A FAIL result from 20 July still open; no workflow, filters, KPIs or pagination |
| SG-47 | Platform | Monthly revenue PKR 35k does not reconcile with per-tenant subscription amounts |
| SG-48 | Org | Branch and Department blank on every user and every driver; only 2 of 9 users in any department |
| SG-51 | GPS / Notifications | `GpsAlertAccess` and `NotificationHub` still authorize platform alert feeds by role name; deferred from SG-05 because replacing them needs alert-category permissions |
| SG-56 | GPS | History screen UX: fake 92% progress bar, details panel over the map and speed buttons, wrong empty state, dead compare state, mislabelled date filter, alert toggle in the filter bar |

### P3 — housekeeping (7)

| ID | Module | Finding |
| --- | --- | --- |
| SG-37 | Menus | Dead entry `/VehicleList` in PascalCase; Fleet Tracking points at `/gps-tracking` while the sidebar uses `/gps-tracking/live`; module slugs mix `access_control`, `vehicle-rental`, `travel-fleet-operations` |
| SG-38 | Menus | Administration module declares 7 items; 3 render. Operations module is visible with 0 items |
| SG-39 | Navigation | WhatsApp is a top-level sidebar group but breadcrumbs read "Administration › WhatsApp" |
| SG-40 | Website | 0 contact requests, 0 demo requests, 0 media items, last published 18 Sep — verify the forms post |
| SG-43 | Platform | Active Companies KPI reads 3 while 5 companies are listed; Expired tenants still counted as active |
| SG-46 | GPS | Driver Performance score weights render empty, so the 51/100 score is computed from invisible weights |
| SG-49 | Dashboard | Greeting reads "Good Afternoon, Admin!" and no company name appears anywhere on the dashboard |
| SG-57 | GPS | History screen accessibility and polish: label contrast 2.43:1, unnamed icon buttons, nested `<main>`, wrapping speed buttons, mixed plate formats |

### Where the findings sit

| Module | P0 | P1 | P2 | P3 |
| --- | --- | --- | --- | --- |
| Platform & Access Control | 4 | 4 | 4 | 3 |
| Finance (invoices, payments, settlements) | 2 | 3 | 2 | 0 |
| GPS & tracking | 1 | 1 | 4 | 1 |
| Fleet (vehicles, drivers, compliance, maintenance) | 1 | 3 | 4 | 0 |
| Operations (bookings, trips, routes) | 0 | 1 | 2 | 0 |
| Integrations & infrastructure | 0 | 2 | 2 | 0 |
| Customers, rental, website | 0 | 1 | 2 | 3 |

## Recommended order of work

The sequence matters more than the list. Fixing dashboards before fixing the ledger produces prettier wrong numbers; fixing the tenant switcher before fixing tenant isolation gives a super admin a cleaner view of leaked data.

### Week 1 — stop the bleeding

- [ ] **Verify the blast radius of SG-01.** Grep every repository and query handler for a missing tenant predicate, starting with anything joining Drivers or Vehicles. This is the only finding that could become a disclosure incident.
- [ ] **Take SG-29 next (right after SG-01).** Remove auto-migrate-on-startup; run migrations only as an explicit gated deploy-pipeline step. Second proof after SG-05: a failed mid-migration (no TX) can leave schema half-applied.
- [ ] **Take /mcp out of production** (SG-22), or put it behind an environment flag. One line of config, removes the largest unreviewed surface.
- [ ] **Mask CNIC in list views and CSV export** (SG-18).
- [ ] **Fix the Vehicles menu permission** (SG-16) — `Vehicle.Create` → `Vehicle.View`.
- [ ] **Clean production data** (SG-35) and stand up a separate seeded demo tenant.
- [ ] **Test the website contact and demo forms end to end** (SG-40). Ten minutes; rules out two months of silently lost leads.

### Weeks 2–6 — make the platform console real

- [ ] **Global tenant switcher in the header, with an All companies option** (SG-06). This single change unblocks Security Center, Audit Center and Workspace Management, and is the foundation for everything else in this block.
- [ ] **Give SUPER\_ADMIN a real permission set and remove the role-name bypass** (SG-05). Add a platform-auditor role that is read-only across all tenants — you will want it for support and for investors.
- [ ] **Make the audit trail readable** (SG-07): platform-wide view, no tenant gate for platform roles, and confirm events are actually being written.
- [ ] **Ship TOTP MFA and turn on password complexity** (SG-08). Until then, relabel the non-enforcing toggles so the console does not overstate what it does.
- [ ] **Include suspended tenants in the selector** (SG-11) so suspension stops being a one-way door.
- [ ] **Cross-tenant user directory** (SG-42).
- [ ] **Move the API to api.sheikhgo.com behind a CDN/WAF** (SG-09), with the base URL in runtime config so the mobile app is not pinned to an IP.

### Weeks 4–10 — close the money chain

This block runs in parallel with the one above and is what turns SheikhGo from a tracking tool into an ERP.

1. Invoice generated automatically on trip completion, with a numbered series per tenant (SG-02).
2. Payments linked to invoices; `PartiallyPaid` moves off the payment and onto the invoice balance (SG-03).
3. Revenue, AR and collected recognised from invoices, not from payment status (SG-14).
4. Scheduled driver settlement run (SG-44).
5. Only then rebuild the dashboard and /reports cards off the ledger, each with a labelled time window (SG-13, SG-15).
6. Enforce subscription state at login so an Expired plan cannot keep 5 users working (part of SG-43).

### Weeks 6–12 — make the fleet data trustworthy

- [ ] **Redefine device states** as Live / Stale / Offline with a visible staleness badge, and never show a cached speed as live (SG-04). Do this before promoting the AI assistant, which will otherwise repeat the error in natural language.
- [ ] **One aggregate KPI query layer** so utilization is not 0% and 100% at once and the vehicle count is the same on every screen (SG-12).
- [ ] **Make compliance work**: mandatory expiry on upload, backfill the 39 documents, drive the dashboard alert from the same data (SG-21).
- [ ] **One state machine** across booking, trip, payment and assignment (SG-17, SG-20).
- [ ] **Fix the enum serialisation contract** once, which clears the Unknown values in allowance rules, compliance and company type (SG-23).
- [ ] **Work down the alert backlog** — 1,795 GPS alerts and 1,748 failed notifications — and set a failure-rate alarm so it never rebuilds (SG-10, SG-32).

### One decision to make first

Everything above assumes SheikhGo is being built as a **multi-tenant SaaS platform** that other fleet operators will buy. If that is the plan, SG-01, SG-05, SG-06, SG-07 and SG-08 are not backlog items — they are the gate on your first enterprise contract, because every buyer's security questionnaire asks exactly those five questions.

If instead the near-term plan is to run SheikhGo for your own operation with two or three friendly tenants, the order changes: the money chain (SG-02, SG-03, SG-14) moves to week 1, because that is what is costing you today, and the platform console work can wait a quarter.
