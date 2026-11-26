# GPS Tracking History audit — fix report

26 Nov 2026 · branch `fix/gps-tracker-clock-skew` · committed locally, **not pushed, not deployed**

The audit of `/gps-tracking/history` listed 19 findings. Fifteen are fixed in code. Three are partly done: two wait on one DNS record, and one has work left on other pages. One (the tracker reporting rate) cannot be fixed in code at all.

Two bugs outside the audit were fixed on the way, because they stopped the screen working: the empty Company dropdown and a migration that failed at every API start.

Nothing reaches production until you deploy. Your local API also needs a restart to pick up the last fixes.

## What you need to do

In this order:

1. **Restart the local API and `ng serve`.** The API picks up the company-selection fix and cookie sessions. `ng serve` must restart because `angular.json` changed; until then `assets/config.json` is not served.
2. **Check it by hand** — steps are in [Verify by hand](#verify-by-hand).
3. **Deploy** the API and the ERP. That is when findings 1–5 are fixed for customers.
4. **Add one DNS record:** `api.sheikhgo.com` → `20.174.1.230` (type A). Caddy is already configured for that name and will issue the certificate itself. Then, in `Frontend/sheikhgo-erp/src/config/production/config.json`, set `"apiUrl": "https://api.sheikhgo.com/api"` and `"authMode": "cookie"`, and redeploy the ERP. This closes findings 6 and 7.
5. **Traccar server settings:**
   - Fix the tracker or Traccar timezone. Devices 19, 23 and 26 report a clock 5 hours off; the API now corrects it on read, but the source is still wrong.
   - Turn on `processing.copyAttributes` for `batteryLevel,rssi`, so battery and GSM appear on every fix instead of about 4% of them.
   - Raise the Jimi VG03 reporting rate (for example every 200 m or 30° of turn). This is finding 10.
6. **Google Cloud:** set the Map ID to vector, which removes the "falling back to raster" warning.
7. **Fix two records in the Vehicles screen:** rename "Toyota Crolla" to "Corolla", and the vehicle named "Ali". Editing a vehicle now also saves its plate in the single format.
8. **Push** when you are happy. Nothing has been pushed.

## The 19 findings

| # | Finding | Status | What changed |
| --- | --- | --- | --- |
| 1 | NON-MOVING overstated | Fixed | The replay window stops at the current time. Moving + non-moving = elapsed window. |
| 2 | STOPS 0 and PARKING 0 | Fixed | When Traccar's stops report is empty, stops are worked out from the route with the same rule as moving time. |
| 3 | AVG SPEED ≠ distance ÷ moving | Fixed | Average speed is distance ÷ moving time, capped at max speed. Max speed also uses Traccar's summary. |
| 4 | ENGINE HOURS = moving time | Fixed | Traccar engine hours, else ignition-on time, else "—". |
| 5 | Live and history 5 hours apart | Fixed, needs deploy | It is the tracker clock skew, not lagging ingestion; replay reads Traccar directly. The correction (commit `3c1e60ca`) was already on this branch. See step 5. |
| 6 | Raw-IP API host | Partly — needs DNS | The host is no longer in the JavaScript bundle; it is read at startup from `assets/config.json`. It still points at the sslip.io address because `api.sheikhgo.com` has no DNS record. |
| 7 | Tokens in localStorage | Built — needs DNS | HttpOnly cookie sessions work and are on in development. Production stays on the token method until step 4, because browsers do not send these cookies across different sites. |
| 8 | ~27 calls for one screen | Partly | `/api/vehicles` 3 → 1, `/api/platform/menus/me` 2 → 1, `/api/smart-alerts` 2 → 1. The dashboard and Live Map calls in the capture belong to pages opened before History and are unchanged. |
| 9 | IMEI unmasked | Fixed in the screen | Shows `•••• 0598` unless the user holds `Gps.DeviceManage`. The API still returns the full value. |
| 10 | 417 fixes in 162 h | Not fixable in code | Needs the tracker reporting rate raised, then road snapping (Google Roads or OSRM). |
| 11 | Progress bar stalls at 92% | Fixed | Indeterminate bar, no percentage, no vendor name. Real progress would need a streamed response. |
| 12 | Details panel covers the map | Fixed | At 1100 px and wider it docks beside the map as a third column. |
| 13 | Wrong empty state | Fixed | "No vehicles match your search" only when there is a search. |
| 14 | Dead compare state | Fixed | The compare chip appears only with two or more vehicles. The dropdown and the list were already in sync. |
| 15 | Date controls | Fixed | "Last 7 Days" ends now, not 23:59 tonight. "Hide records before" is now "Hide stops before", which is what it does. |
| 16 | Alert toggle in the filter bar | Moved | Now under Alerts → Rules, in neutral styling. It is still a browser-only notification. |
| 17 | Timeline ticks without dates | Fixed | Labels include the date when the span is over 24 hours. |
| 18 | Contrast failures | Fixed | Labels are `#4b5563` at 11 px (was 2.43:1). |
| 19 | Polish | Mostly fixed | Icon buttons named; one `<main>`; speed buttons on one row; zoom control clear of the assistant button; altitude 0 shows "—"; Export and Share explain themselves; plates saved in one format. Outside the repo: the vector Map ID, battery/GSM, the two data records. |

## Fixed outside the audit

- **Empty Company dropdown, then 403 on everything.** Four queries filtered on `Tenants.IsDeleted`, a column that does not exist.
  - Three made the company list, the license summary and the revenue dashboard return 500.
  - The fourth validates the selected company. It failed silently, so once a company was selected every request was refused with 403. This one was found last, during the browser check, and **your running API does not have it yet** (step 1).
- **Revenue dashboard.** `FeaturesLocked` now maps correctly, and "oldest due date" uses the oldest date.
- **Startup migration error** `Invalid column name 'GroupId'` in the Umrah migration. The column is now added in its own batch. This file belongs to the TravelNexus work.
- **Vehicle filter was wiped on every GPS page open.** The same double subscription that caused the three `/api/vehicles` calls also cleared the saved filter. It now survives a reload.

## Not verified

- **Cookie login in a real browser against the API.** Your API on port 5082 was running the older build. I did not restart it, and did not start a second copy, because a second copy would run the Traccar sync and WhatsApp workers twice against the same database. Cookie sessions are covered by 25 backend tests (six through the real ASP.NET authentication middleware) and 30 ERP tests.
- **The History screen with a real route loaded.** API calls from the page returned 403 until the restart. The new layout rules were checked on the live page instead: three columns when details are open, the panel beside the map and not over it, one `<main>`, label colour and size.
- **Production.** Nothing is deployed.

## Verify by hand

After step 1:

1. Sign in as SUPER_ADMIN. The header Company dropdown lists companies. Pick one.
2. Open GPS → History, pick vehicle 50, choose **Last 7 Days**, load the route.
   - MOVING + NON-MOVING equals the elapsed window.
   - AVG SPEED ≈ DISTANCE ÷ MOVING.
   - STOPS and PARKING are above 0.
   - The Details panel sits beside the map; all six speed buttons are reachable.
   - The loading bar has no percentage.
3. In the browser's network tab, reload the page: one `/api/vehicles`, one `/menus/me`, one `/smart-alerts`.
4. Cookie login: sign out and in again. In DevTools → Application, Local Storage has no `access_token` or `refresh_token`, and Cookies shows `sg_at` and `sg_rt` marked HttpOnly. Live GPS still updates. Sign out removes both cookies.
5. Mobile contract: `POST /api/auth/login` with only `email` and `password` still returns both tokens in the JSON.
6. Sign in as a user without `Gps.DeviceManage`: the vehicle card shows a masked IMEI.

## Tests

| Suite | Result |
| --- | --- |
| Backend | 2,248 passed, 1 failed, 6 skipped |
| ERP | 239 passed, 2 failed |

- The three failures were failing before this work and are not caused by it: `AlertModuleCatalogTests` (the alert catalog has no `harsh_braking` entry), and the ERP specs `gps-address.util` and `tracker-status.util`.
- The six skipped backend tests need a database connection. The four for this work pass when run against the local database.

## Commits

Twelve, oldest first:

| Commit | What |
| --- | --- |
| `55d335fa` | fix(SG-53): history replay KPIs reconcile with the elapsed window |
| `33e7987e` | fix(SG-11): tenant queries no longer filter on a missing Tenants.IsDeleted column |
| `43b07326` | fix(travelnexus): add TravelVisaCases.GroupId in its own batch |
| `615ffd9e` | fix(SG-56): GPS history screen UX |
| `a622f373` | fix(SG-57): GPS history accessibility and polish |
| `ae3e20b6` | fix(SG-57): normalise vehicle plate format at entry |
| `9941bea4` | fix(SG-55): mask IMEI on the history vehicle card |
| `713b8334` | fix(SG-34): stop duplicate vehicle, menu and smart-alert requests |
| `75fae4cb` | fix(SG-09): load the API host from runtime config |
| `9b795457` | fix(SG-54): HttpOnly cookie sessions for the ERP |
| `f88a4c72` | fix(SG-11): selected-company check no longer filters on Tenants.IsDeleted |
| this commit | docs(audit): register SG-53–SG-57 and add this report |

No database migration was added. Parts touched: Backend and ERP. The Flutter apps were not changed; the login and refresh contract they use is the same, with one optional new field.

## Left open

- **`GET /api/auth/me` returns 500.** The query returns seven columns and `UserDto` now has more, so the result cannot be mapped. The Flutter driver and fleet apps call this endpoint; the ERP does not. Not part of this audit and not changed.
- **Server-side IMEI masking.** The screen masks it; the API still sends it.
- **Stationary > 24 h alert** is still checked in the browser only, while GPS History is open. A real alert needs a server-side rule.
- **Live Map** repeats its fleet-status and fleet-health calls. Remaining SG-34 work.
- **Flutter and Fleet apps** still have the sslip.io host as their build default.
- **Existing plates** keep their old format until the vehicle is edited.
