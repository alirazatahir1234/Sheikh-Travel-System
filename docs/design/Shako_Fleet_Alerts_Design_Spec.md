# Shako Fleet Alerts Design Spec

| Field | Value |
| --- | --- |
| **Document title** | Shako Fleet Alerts Design Spec |
| **Product / brand** | **SheikhGo** (mobile fleet ops app). Users often say “Shako”; treat as informal alias for SheikhGo. Tenant shown in screenshot: **Sheikh Travel**. |
| **Surface** | Flutter mobile — fleet-owner / admin shell (`Frontend/sheikh-driver`) |
| **Screen name (UI)** | Fleet Alerts |
| **Route** | `/alerts` |
| **Entry** | Bottom nav **More** → menu item **Fleet Alerts** |
| **Version** | 1.0 |
| **Status** | Design specification (for UI generation / redesign) |
| **Date** | 2026-11-01 |
| **Audience** | Product, design, engineering; pasteable into ChatGPT / design tools to generate a new UI |

---

## 1. Purpose & goals

### 1.1 Purpose
Give fleet owners and admins a single, glanceable place to **see, triage, and act on live GPS / telematics fleet alerts** for their company (e.g. Sheikh Travel), without cluttering the list with already-handled history.

### 1.2 Goals
1. Show **active** alerts clearly, with **priority color coding** (critical / warning / info).
2. Keep the **active list clean** by moving **resolved** alerts into a **separate tab**.
3. Make every alert **actionable**: relative time, vehicle number, location tag, tap-through to the vehicle’s **live map**.
4. Provide **simple filters** for Critical, Warning, and Resolved (Resolved via its own tab + optional chips).
5. Never show a **broken blank body** — loading, error, and empty states must always be intentional and visible.
6. Match SheikhGo brand: navy + teal, clean mobile list UI (not a dense desktop dashboard).

### 1.3 Non-goals
- **GPS Commands** are **not** on this page (separate More entry → `/gps/commands`).
- Full incident investigation workspace (Incident center is separate: `/gps/incidents`).
- Creating alert rules / geofence configuration (settings / GPS modules elsewhere).
- Driver-facing trip alerts UX (this is fleet-owner / admin alerts).
- Push-notification preference management (Notifications screen is separate: `/notifications`).
- Bulk enterprise workflows beyond acknowledge / assign / resolve (keep secondary).

---

## 2. Branding & naming

| Context | How to show it |
| --- | --- |
| App brand mark | **Sheikh** (navy `#071D2E`) + **Go** (teal `#0D9488`) wordmark in header |
| Screen title | **Fleet Alerts** (large, bold navy) |
| Informal product name in docs | **Shako Fleet Alerts** (= SheikhGo Fleet Alerts) |
| Tenant subtitle | `{CompanyName} • {N} vehicles` e.g. `Sheikh Travel • 4 vehicles` |
| Bottom nav | Overview · Map · Vehicles · Drivers · **More** (active when opened from More) |

Visual direction: modern fleet ops mobile, cool light grey surface (`#F2F6F8` / `#F3F7F8`), white list rows, teal accents for selected chrome, red badges for unread counts. Avoid purple gradients, cream-serif newspaper looks, or heavy card stacks in the hero/header.

---

## 3. User personas

### 3.1 Fleet owner (primary)
- Runs a small–mid fleet (e.g. 4–50 vehicles under Sheikh Travel).
- Opens More → Fleet Alerts when something feels wrong or after a push/badge.
- Needs: “What’s on fire right now?”, which vehicle, where, how long ago, and jump to map.
- Rarely needs Commands from this screen.

### 3.2 Fleet admin / dispatcher (primary)
- Triages alerts during the day: acknowledge, assign, mark resolved.
- Filters to Critical when overloaded; reviews Resolved later for audit.
- Needs relative times and clear severity colors under time pressure.

### 3.3 Explicitly not this page’s job
- Sending GPS device commands (immobilize, etc.) → **GPS Commands**.
- Deep maintenance ticket creation → Maintenance modules.
- Driver chat / trip acceptance → other modules.

---

## 4. Information architecture & navigation

### 4.1 How the user reaches Fleet Alerts
1. Launch SheikhGo fleet shell (authenticated).
2. Tap bottom tab **More** (3×3 grid; may show red badge e.g. `99+`).
3. Tap **Fleet Alerts** (`Icons.notifications_active_outlined`) → navigate to **`/alerts`**.
4. Optional deep links:
   - From a vehicle detail **Alerts** tab → `/alerts?vehicleId={id}` (or equivalent) with vehicle filter applied.
   - From a push notification → open alert detail or list focused on that alert.

### 4.2 Related destinations (not children of this page)
| Destination | Route (existing) | Relationship |
| --- | --- | --- |
| Notifications | `/notifications` | Bell in header |
| Fleet live map | `/fleet/map` | Tap vehicle / location on alert |
| Vehicle detail | vehicle deep link / fleet vehicle route | Optional secondary |
| GPS Commands | `/gps/commands` | Sibling under More — **out of scope here** |
| Incident center | `/gps/incidents` | Sibling for heavier incidents |
| More hub | More tab | Parent entry point |

### 4.3 In-page IA
```
Fleet Alerts
├── Header (brand + title + company/vehicles + bell)
├── Segmented tabs: [ Active ] [ Resolved ]
├── Severity / status filter chips (context-sensitive)
├── Optional: search + date range + sort (secondary)
└── Scrollable alert list
    ├── Alert cards / rows
    ├── Empty state (intentional)
    ├── Loading / error / pull-to-refresh
    └── (optional) bulk action bar when selecting
```

---

## 5. Current state / known issues (blank page diagnosis)

### 5.1 Observed symptom (screenshot, 2026-11-01)
- Header chrome **renders correctly**: SheikhGo wordmark, title **Fleet Alerts**, subtitle **Sheikh Travel • 4 vehicles**, notification bell with red dot.
- Bottom nav **More** is active (teal pill) with **99+** badge.
- **Main body is empty white / pale grey** — no KPI row, no chips, no list, no empty-state illustration, no error message, no loader.
- This is **not** a full app crash: shell + header remain; content region fails to paint intentional UI.

### 5.2 Implementation locus
- App: `Frontend/sheikh-driver` (fleet-owner Flutter shell).
- Screen: `lib/features/alerts/presentation/alerts_screen.dart` → `AlertsScreen`.
- Header: `widgets/fleet_alerts_header.dart` (`FleetAlertsHeader`) — matches screenshot.
- List row: `widgets/fleet_alert_row.dart`.
- State: Riverpod `alertsProvider` / `alerts_notifier.dart`.
- API: GPS alerts events/stats (`gps_alerts_api.dart`), typically `/api/gps/alerts/events` + stats.

### 5.3 Diagnosis findings (agent investigation + code inspection)
Investigation transcript: [Blank Shako fleet alerts](216066c4-cdf8-479d-b403-2ccb1064ac2b) (incomplete at time of this spec; findings below combine that work with static review).

**Evidence-based conclusions / strong hypotheses:**

1. **Chrome vs body split**  
   `FleetAlertsHeader` is built **outside** `async.when(...)`. The body is an `Expanded` child of `async.when(loading / error / data)`. A blank white body with a healthy header means the shell is fine; the failure is inside the **async body branch** (or a child that throws during layout/build after data arrives).

2. **Not a “full navigation miss”**  
   Route `/alerts` is registered; More menu entry **Fleet Alerts** is visible. User reached the correct screen.

3. **Empty-state text exists but is weak / may never show**  
   Current empty copy is a plain centered string: *“No alerts match these filters”* (`SliverFillRemaining`). Even when data is empty, the design **must** show intentional empty UI (illustration + title + CTA). Today’s empty state is easy to miss and looks “broken” on a large pale field. The screenshot looks **worse than empty**: filters/KPIs also missing, so this is likely **not** a successful empty render.

4. **Layout-exception blanking pattern (high likelihood)**  
   Elsewhere in the same app (Fleet Hub), comments document that **Badge + unbounded width** can crash layout and **blank the list** while chrome remains. Fleet Alerts uses `Badge` around filter `IconButton` and KPI cards use **`Spacer()` inside a `Column`** within a fixed-height horizontal `ListView` child (`_KpiCard`). Diagnosis was actively checking whether a **layout exception in the `data:` branch** (constraints / Spacer / Badge) aborts painting of KPIs + list, leaving pure scaffold background.

5. **Data path may still succeed**  
   Local API probes during diagnosis suggested events/stats can return successfully for the Sheikh Travel admin user. Blank UI is therefore **not exclusively** “zero alerts from API”; treat layout/runtime exceptions and swallowed errors as first-class suspects.

6. **Gaps vs desired product UX**  
   - No dedicated **Active | Resolved** top tabs (Resolved is only a chip among many).  
   - List shows **clock time** (`formatAlertClock`) more than **relative** (“2 minutes ago”) though `formatAlertRelativeTime` already exists.  
   - Vehicle meta is present; **tappable location tag → live map** is not a first-class card affordance.  
   - Priority colors exist in code (`AppColors.error` / `warning` / `info`) but the blank screen never surfaces them.

### 5.4 What “fixed” means for the blank body (engineering notes for redesign)
- Always paint **loading**, **error + Retry**, or **intentional empty** under the header.
- Guard list chrome (KPI, chips, search) so a list layout error cannot wipe the entire body.
- Prefer bounded layouts (no unbounded `Spacer` / `Badge` width traps).
- Empty must never look like “missing UI.”

---

## 6. Layout specification

### 6.1 Screen scaffold
- `Scaffold` background: light cool grey (`SgPalette.light.surface` ≈ `#F2F6F8`; current screen uses `#F3F7F8`).
- `SafeArea` top; bottom nav owned by shell (leave space so last card isn’t hidden).
- Vertical structure (top → bottom):

```
┌─────────────────────────────────────┐
│ Status bar (system)                 │
├─────────────────────────────────────┤
│ HEADER                              │
│  [back?]              [bell •]      │
│  SheikhGo                           │
│  Fleet Alerts                       │
│  Sheikh Travel • 4 vehicles         │
├─────────────────────────────────────┤
│ TABS:  Active (12)  |  Resolved (3) │
├─────────────────────────────────────┤
│ FILTER CHIPS (horizontal scroll)    │
│  All · Critical · Warning · Info    │
│  (+ Resolved chip only on Active?   │
│     or hide Resolved chip; tab owns │
│     resolved)                       │
├─────────────────────────────────────┤
│ Optional secondary row:             │
│  [Search…………] [Tune filters]        │
│  [Today • date] [Newest first]      │
├─────────────────────────────────────┤
│ LIST / EMPTY / LOADING / ERROR      │
│  (scroll + pull-to-refresh)         │
├─────────────────────────────────────┤
│ Optional bulk bar (select mode)     │
├─────────────────────────────────────┤
│ Bottom nav (shell)                  │
└─────────────────────────────────────┘
```

### 6.2 Header
- **Brand:** SheikhGo rich text (Sheikh navy, Go teal), ~22pt bold.
- **Title:** “Fleet Alerts”, ~26pt extrabold navy.
- **Subtitle:** `{company} • {count} vehicles`, 13pt secondary grey.
- **Bell:** top-right; red dot or count if unread notifications; navigates to `/notifications`.
- **Back:** only if route can pop (deep link); else omit so More-shell entry feels like a root page.
- Do **not** put Commands, Map, or KPI clutter in the header.

### 6.3 Tabs — Active vs Resolved (required)
| Tab | Content |
| --- | --- |
| **Active** | Open / unacknowledged / acknowledged-but-not-resolved alerts. Default landing tab. |
| **Resolved** | Resolved (and optionally archived) alerts. Keeps Active clean. |

- Segmented control or Material 3 style tabs under header.
- Show **counts** on tabs when known: `Active (12)`, `Resolved (48)`.
- Switching tabs resets severity chip to **All** for that tab (or preserves last chip per tab — prefer remember per-tab filter).
- Resolved tab default sort: newest resolved first.
- Visual: selected tab uses teal underline or filled pill; unselected muted.

### 6.4 Filter chips
On **Active** tab, horizontal chips:
- **All** (default)
- **Critical** (red accent when selected)
- **Warning** (amber)
- **Info** (blue)
- Optional secondary chips: **Unacknowledged**, **Assigned to me** (if product needs them; keep below severity chips or in filter sheet)

On **Resolved** tab:
- Chips may be **All / Critical / Warning / Info** filtered within resolved set, **or** simply date-focused (Today / Last 7 days). Do **not** rely on a “Resolved” chip on Active as the only way to see history.

**Filter sheet (tune icon):** date preset (Today, Last 7, Last 30, All time), vehicle, driver, event type — advanced; keep severity chips visible without opening the sheet.

### 6.5 List
- Compact **rows** preferred over large marketing cards (matches existing `FleetAlertRow` intent).
- Grouping optional: “Needs attention” then “Earlier today” / grouped incidents — nice-to-have; don’t block v1.
- Infinite scroll or “Load more” at bottom if paged.
- Pull-to-refresh always available (even on empty).

### 6.6 Loading
- Under header (tabs may skeleton too): centered **SgBrandLoader** with message **“Loading fleet alerts…”**.
- Prefer skeleton for KPI/chips/list after first successful load (`skipLoadingOnReload` pattern).

### 6.7 Error
- Icon + short message + **Retry** button.
- If stats fail but list succeeds: inline warning banner *“Stats unavailable — showing alert list.”* (already in code) — keep.

---

## 7. Alert card anatomy

Each list item must communicate priority, what happened, when, which vehicle, and where — at a glance.

```
┌─[sev stripe]──────────────────────────────────────────────┐
│ [●/icon]  Overspeed                         2 minutes ago │
│           LEB-8825  ·  Near Exit 12, Riyadh     WARNING   │
│           Exceeded 120 km/h limit (132 km/h)    ○ Open    │
└───────────────────────────────────────────────────────────┘
```

### 7.1 Required elements
| Element | Spec |
| --- | --- |
| **Priority stripe or dot** | 3–4px left border **or** severity dot + icon tile tinted by severity |
| **Title** | Human event title e.g. Overspeed, Vehicle Offline, SOS / Alarm, Geofence Exited |
| **Message** | One-line supporting detail (limit, duration, geofence name); max 2 lines ellipsis |
| **Relative timestamp** | e.g. `Just now`, `2 minutes ago`, `3h ago`, `Yesterday`, else short date — **required** (not only HH:mm) |
| **Vehicle number** | Plate / registration preferred (`LEB-8825`), else vehicle name, else `Vehicle #id` — **tappable** |
| **Location tag** | Short place label or reverse-geocode snippet / lat-lng fallback — **tappable** with map pin icon |
| **Status** | Active: Unacknowledged / Acknowledged / Assigned · Name; Resolved: Resolved · relative time |
| **Severity label** | CRITICAL / WARNING / INFO (and RESOLVED styling on Resolved tab) |
| **Chevron** | Indicates drill-in |

### 7.2 Optional elements
- Driver name as secondary meta (`Vehicle • Driver`).
- Group badge for multi-event incidents (layers icon).
- Selection checkbox in multi-select mode.

### 7.3 Visual density
- Row vertical padding ~10–12px; icon tile 40×40 rounded 10.
- Divider `#E8EEF2` between rows; rows sit on white or transparent over surface.
- Do **not** use heavy elevated cards for every row.

---

## 8. Color tokens & severity mapping

### 8.1 Brand / surface (align with `AppColors` / `SgPalette`)
| Token | Light | Usage |
| --- | --- | --- |
| Navy | `#071D2E` | Titles, primary text emphasis |
| Teal / Primary | `#0D9488` | Active tab, selected chips, brand “Go”, FAB accents |
| Surface | `#F2F6F8` | Page background |
| Card / row | `#FFFFFF` | List background |
| Text secondary | `#64748B` | Subtitles, meta |
| Text muted | `#8B97A5` | Timestamps |
| Border | `#D5DEE7` | Chips, buttons |
| Success | `#10B981` | Resolved / acknowledged positive |

### 8.2 Severity (required mapping)
| Severity | Color token | Hex (approx) | Tint background | Meaning |
| --- | --- | --- | --- | --- |
| **Critical** | Error / red | `#EF4444` | `#FEE2E2` | SOS, power cut, urgent safety |
| **Warning** | Amber | `#F59E0B` | `#FFEDD5` / `#FEF3C7` | Overspeed, offline, geofence, harsh events |
| **Info** | Blue | `#2563EB` | `#DBEAFE` | Ignition, back online, informational |
| **Resolved** | Success green | `#10B981` | soft green wash | Shown on Resolved tab / recovered offline |

### 8.3 Dark mode
App supports `SgPalette.dark`. Spec:
- Keep **severity hues** recognizable (red/amber/blue) — do not desaturate below WCAG contrast on dark cards.
- Surfaces: dark `#0E141B`, cards `#171E27`, text light.
- Selected teal may use `primaryLight` `#14B8A6` on dark.
- Empty-state illustrations should have dark variants or use monotone icons.

---

## 9. Interactions

### 9.1 Tap behaviors
| Target | Action |
| --- | --- |
| **Alert row (main area)** | Open **alert detail** (bottom sheet or full screen): full message, map preview, timeline, actions Acknowledge / Assign / Resolve |
| **Vehicle number** | Navigate to **that vehicle on Fleet live map** (`/fleet/map` focused on `vehicleId`) — do **not** require opening detail first |
| **Location tag** | Same as vehicle → **live map** centered on alert lat/lng and/or vehicle |
| **Severity chip** | Filter list; tap again on selected chip → All (toggle) |
| **Active / Resolved tab** | Switch datasets; keep Active free of resolved noise |
| **Bell** | `/notifications` |
| **Pull down** | Refresh events + summary |
| **Long-press row** | Enter multi-select (optional; existing pattern) |
| **Mark resolved** | From detail and/or swipe / overflow; on success, item leaves Active and appears under Resolved |
| **Acknowledge** | From detail or bulk bar (permission-gated) |

### 9.2 Mark resolved flow
1. User opens detail → **Mark resolved** (optional notes).
2. Optimistic UI: remove from Active list; toast “Alert resolved”.
3. Item available under Resolved tab immediately.
4. Undo optional (nice-to-have).

### 9.3 Live map focus contract
When navigating from vehicle/location tag:
- Pass `vehicleId` (required) and optional `lat`, `lng`, `alertId`.
- Map should select/highlight the vehicle and center camera.
- Back returns to Fleet Alerts preserving tab + filters.

---

## 10. Filter & sort behavior

### 10.1 Defaults
- Tab: **Active**
- Chip: **All**
- Date: **Today** (or Last 7 if Today often empty — product choice; document choice in UI via date button)
- Sort: **Newest first**

### 10.2 Chip logic (Active)
- **Critical** → only critical severity display bucket  
- **Warning** → warning bucket  
- **Info** → info bucket  
- Combining tab Resolved + chip Critical → critical-among-resolved  

### 10.3 Sort
- Toggle Newest / Oldest (existing `FleetAlertSort`).
- On Resolved tab, sort by `resolvedAt` when available, else `timestamp`.

### 10.4 Search
- Query matches vehicle plate/name, driver, alert title/message.
- Empty search results → **filtered empty** state (not blank).

### 10.5 Vehicle filter chip
- If opened with `vehicleId`, show dismissible chip `Vehicle #…` / plate.

---

## 11. Empty, loading, and error states (critical)

The current blank screenshot must **never** be an acceptable end state.

### 11.1 No active alerts (Active tab, no filters)
- Illustration or large muted icon (bell-check / shield).
- Title: **All clear**
- Body: **No active fleet alerts right now.**
- Secondary: `Sheikh Travel • {N} vehicles monitored`
- CTA: **View resolved** (switches tab) + **Refresh**

### 11.2 No resolved alerts (Resolved tab)
- Title: **No resolved alerts yet**
- Body: **Resolved alerts will show up here for your records.**
- CTA: **Back to Active**

### 11.3 Filtered empty (any tab)
- Title: **No matches**
- Body: **No alerts match these filters.**
- CTA: **Clear filters** (resets chips/search; keeps tab)

### 11.4 Loading
- Never blank white: show loader or skeletons for chips + 6 row placeholders.

### 11.5 Error
- Never blank white: icon, message, **Retry**.
- Distinguish network vs permission if possible (“You don’t have access to fleet alerts”).

### 11.6 Design rule
Header + tabs should remain visible in empty/error states so the page never looks like a failed route.

---

## 12. Accessibility

- Severity is **not color-only**: include text label CRITICAL / WARNING / INFO and/or icon.
- Minimum tap targets 44×44 for chips, vehicle/location links, bell.
- Dynamic type: title and message should scale; truncate gracefully.
- Screen reader: row announcement e.g. “Critical. Overspeed. Vehicle LEB-8825. 2 minutes ago. Near Exit 12. Double tap for details. Vehicle number opens live map.”
- Contrast: red/amber/blue text on tinted chips ≥ WCAG AA against their backgrounds.
- Reduce motion: skip non-essential list entrance animations if OS requests.

---

## 13. Suggested data model (UI-facing)

```ts
type AlertSeverity = 'critical' | 'warning' | 'info';
type AlertLifecycle = 'active' | 'resolved' | 'archived';

interface FleetAlert {
  id: number;
  vehicleId: number;
  vehicleNumber: string;      // plate or display label
  vehicleName?: string;
  driverName?: string;
  eventType: string;          // e.g. speed_exceeded, vehicle_offline, sos
  title: string;              // localized display title
  message: string;            // short body
  severity: AlertSeverity;
  status: AlertLifecycle | string;
  timestamp: string;          // ISO UTC
  relativeTime: string;       // computed client-side
  latitude?: number;
  longitude?: number;
  locationLabel?: string;     // "Near Exit 12, Riyadh" or geofence name
  geofenceName?: string;
  speed?: number;
  isAcknowledged: boolean;
  assignedToName?: string;
  resolvedAt?: string;
  resolvedBy?: string;
  canAcknowledge: boolean;
  canResolve: boolean;
  canAssign: boolean;
}
```

**Summary / KPI model**
```ts
interface FleetAlertsSummary {
  critical: number;
  warning: number;
  info: number;
  activeTotal: number;
  resolvedTotal: number;
  unacknowledged: number;
}
```

---

## 14. API / behavior assumptions (high-level)

- List sourced from GPS alert events API (existing SheikhGo backend), e.g. paged `GET /api/gps/alerts/events` with `datePreset`, `sort`, severity/status filters.
- Stats/summary from companion stats endpoint; UI tolerates stats failure.
- Auth: JWT fleet session; menu gated by alerts permission (`_alertsMenu` / GPS alert permissions).
- Acknowledge / assign / resolve via existing mutation endpoints; UI respects `canAcknowledge`, `canResolve`, `canAssign`.
- Relative times computed on device from `timestamp` / `resolvedAt`.
- Location label: prefer `geofenceName`, else reverse geocode cache, else abbreviated coordinates.
- Real-time: pull-to-refresh required; optional SignalR/push later to bump badge + prepend Active list (out of band for visual design).

---

## 15. Content & copy guidelines

- Tone: calm, operational, short.
- Prefer “Vehicle Offline” over raw `vehicle_offline`.
- Relative time examples: `Just now`, `2 minutes ago`, `15 minutes ago`, `3 hours ago`, `Yesterday`, `12 Oct`.
- Empty states always complete sentences; never leave body with only whitespace.
- Errors: human, not stack traces (`Couldn't load fleet alerts. Check your connection and try again.`).

---

## 16. Motion (subtle)

- Tab switch: cross-fade or short slide (≤200ms).
- Pull-to-refresh: standard platform indicator in teal.
- New critical alert appearing (if live): brief severity stripe pulse once — optional.
- Avoid continuous badge bounce / glow.

---

## 17. Acceptance criteria checklist

- [ ] Screen titled **Fleet Alerts** with SheikhGo brand + `{Company} • {N} vehicles`.
- [ ] Reachable from **More → Fleet Alerts** (`/alerts`).
- [ ] **Active** and **Resolved** tabs present; Active excludes resolved items.
- [ ] Severity colors: **red critical**, **amber warning**, **blue info**.
- [ ] Each alert shows **relative timestamp**.
- [ ] Each alert shows **vehicle number** and **location tag**.
- [ ] Tap vehicle number or location → **live map** for that vehicle / point.
- [ ] Tap row → detail with resolve / acknowledge as permitted.
- [ ] Filters for **Critical** and **Warning** (and Info); Resolved via tab.
- [ ] Pull-to-refresh works on list and empty states.
- [ ] Empty states are intentional for: no active, no resolved, filtered empty.
- [ ] Loading and error states never render a blank white body under the header.
- [ ] Commands **not** shown on this page.
- [ ] Light + dark (`SgPalette`) readable severity treatment.
- [ ] Accessibility: severity not color-only; 44px targets; TalkBack/VoiceOver labels.

---

## 18. Out of scope

- GPS Commands UI (`/gps/commands`).
- Building alert rules / notification preference editors.
- Full Incident Center redesign.
- ERP / Angular GPS alerts web console (`sheikhgo-erp`) — separate surface (may share severity language).
- Driver-only app flows unrelated to fleet-owner shell.
- Implementing backend new endpoints (design assumes existing GPS alerts APIs).
- Marketing website GPS Alerts landing pages.

---

## 19. Design prompts (for ChatGPT / image UI tools)

Use this brief when generating mockups:

> Design a mobile Flutter-style screen for **SheikhGo Fleet Alerts** (informally “Shako”). Light cool-grey background, navy+teal branding. Header: SheikhGo wordmark, title “Fleet Alerts”, subtitle “Sheikh Travel • 4 vehicles”, bell with red dot. Segmented tabs Active | Resolved. Horizontal filter chips Critical (red) / Warning (amber) / Info (blue). Compact alert rows with left severity stripe, title, message, relative time (“2 minutes ago”), tappable vehicle plate and location pin tag. Show a polished empty state for Active (“All clear”) — never a blank white body. Bottom nav with More selected. No GPS Commands on this screen. Provide variants: Active with mixed severities, Active empty, Resolved list, filtered empty, loading skeletons, error+retry.

### Sample Active list content (for mock data)
1. **CRITICAL** — SOS / Alarm — `LEB-1204` — `Industrial Area Gate` — `2 minutes ago`  
2. **WARNING** — Overspeed — `LEB-8825` — `Near Exit 12, Riyadh` — `15 minutes ago`  
3. **WARNING** — Vehicle Offline — `LEB-4410` — `Last: King Fahd Rd` — `1 hour ago`  
4. **INFO** — Ignition On — `LEB-8825` — `Depot` — `3 hours ago`  

---

## 20. Traceability to current codebase (for implementers)

| Concern | Current reference |
| --- | --- |
| Screen | `Frontend/sheikh-driver/lib/features/alerts/presentation/alerts_screen.dart` |
| Header | `.../widgets/fleet_alerts_header.dart` |
| Row | `.../widgets/fleet_alert_row.dart` |
| Relative time helper | `formatAlertRelativeTime` in `domain/alert_display.dart` (prefer over clock-only in list) |
| Colors | `core/constants/app_theme.dart` — `AppColors`, `SgPalette` |
| Nav entry | `core/navigation/fleet_nav_config.dart` — More → Fleet Alerts → `/alerts` |
| Live map sibling | More → Fleet live map → `/fleet/map` |
| Commands (exclude) | `/gps/commands` |

---

## 21. Revision history

| Version | Date | Notes |
| --- | --- | --- |
| 1.0 | 2026-11-01 | Initial Shako / SheikhGo Fleet Alerts design spec; includes blank-page diagnosis summary and redesign requirements (Active/Resolved tabs, relative time, vehicle+location → map, intentional empty states). |

---

*End of document.*
