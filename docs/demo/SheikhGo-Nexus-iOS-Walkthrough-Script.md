# SheikhGo Nexus — iOS mobile demo script

Use for **live narration** (QuickTime screen record on Simulator, or `tools/record-ios-mobile-demo.sh` for a silent automated pass).

**Target length:** 5–7 minutes · **Device:** iPhone 17 Pro Max (Simulator or physical) · **App:** `Frontend/sheikh-driver` (SheikhGo Fleet / Tracking, bundle `com.sheikhgo.fleet`)

**Demo login (fleet manager / owner ops):** `admin@sheikhtravel.com` / `Pass@123`  
**Driver-only pass (optional second act):** phone `03021234567` / `Pass@123` (requires seeded driver linked in tenant)

**API:** Production demo host (`ENV=prod` or `./scripts/run_prod_device.sh`) — same backend as the web Nexus ERP.

---

## 0:00 — Splash & brand (0:20)

1. Cold-launch the app (kill from app switcher first).
2. Hold on splash: SheikhGo logo, navy gradient.
3. **Say:** “SheikhGo Fleet is the mobile layer of Nexus — one API with the web ERP, tuned for owners and drivers in the field.”

---

## 0:20 — Login (0:35)

1. Login hero: “SheikhGo Tracking — Owners track the fleet · Drivers run trips.”
2. Enter work email and password → **Continue**.
3. Point out resolved API URL on debug builds (optional).
4. **Say:** “Same tenant and roles as Nexus — permissions drive what each user sees on the bottom bar.”

---

## 0:55 — Command dashboard (1:00)

1. **Dashboard** tab — KPI strip, fleet status, critical alerts, quick actions.
2. Scroll if needed — AI attention card, recent activity.
3. **Say:** “The command dashboard mirrors ERP priorities: what needs attention now, not a static home screen.”

---

## 1:55 — Live map (1:15)

1. Tap **Live Map**.
2. Pan/zoom; mention vehicle markers and live status (if map tiles load).
3. **Say:** “Live map is a first-class tab for fleet managers — same SignalR tracking hub as the web live map.”

---

## 3:10 — Vehicles & detail (1:00)

1. **Vehicles** tab — fleet list / hub.
2. Open one vehicle → live map sheet, history, or alerts tab if available.
3. Back to list.
4. **Say:** “Drill-down is vehicle-centric — history, alerts, and playback align with GPS modules in Nexus.”

---

## 4:10 — Trips (0:45)

1. **Trips** — operational trip queue / GPS trips entry.
2. Open one row if data exists.
3. **Say:** “Trips connect dispatch to the driver app — start, complete, and location breadcrumbs on the shared API.”

---

## 4:55 — More menu (1:15)

1. **More** — scroll the role-based menu.
2. Highlight 2–3 items your tenant shows, e.g. **Alerts**, **GPS commands**, **Geofences**, **Fuel analytics**, **Reports**, **Notifications**.
3. Open **Settings** — theme, language (English / Arabic RTL), security (biometric lock).
4. **Say:** “Everything else rolls into More — GPS operator tools, finance, and reports follow the same permission model as the ERP shell.”

---

## 6:10 — Optional: driver role (0:50)

1. Sign out → log in as driver phone `03021234567`.
2. Show driver tabs: **Dashboard**, **Trips**, **GPS**, **Inbox**, **Profile**.
3. **Say:** “Drivers get a focused five-tab shell — trips, background GPS, attendance, and inbox without owner-only fleet admin.”

---

## 7:00 — Close (0:10)

**On screen:** Dashboard or Live Map  
**Say:** “SheikhGo Nexus — web ERP, iOS fleet app, one platform. Request a demo for your workflow.”

---

## Recording commands

| Tool | Action |
|------|--------|
| Boot Simulator | `open -a Simulator` |
| Run app (local API) | `./scripts/run_ios_sim.sh` from `Frontend/sheikh-driver` |
| Run app (prod API) | `flutter run -d "iPhone 17 Pro Max" --dart-define=ENV=prod` |
| Automated MP4 | `chmod +x tools/record-ios-mobile-demo.sh && ./tools/record-ios-mobile-demo.sh` |
| Manual QuickTime | File → New Screen Recording → select Simulator window |
| Physical iPhone | Xcode → Devices → `flutter run -d <udid> --dart-define=DEV_LAN_HOST=$(ipconfig getifaddr en0)` |

Output video (after automated record):  
`Frontend/sheikhgo-erp/public/website/demo/sheikhgo-nexus-ios-walkthrough.mp4`

---

## Covered vs optional

| Covered in script | Often skipped in short cut |
|-------------------|----------------------------|
| Splash + login | Forgot password flow |
| Owner/fleet manager bottom nav | Every GPS operator sub-screen |
| Live map + vehicles + trips | Bookings-only dispatcher layout |
| More menu + settings | Biometric lock demo |
| Driver role (optional) | Offline queue / fuel receipt capture |
| Arabic RTL toggle | App Store / TestFlight build |

## Repo notes

| Path | Role |
|------|------|
| `Frontend/sheikh-driver` | **Primary** Flutter app (package `sheikh_go_driver`, bundle `com.sheikhgo.fleet`) |
| `Frontend/sheikhGo-Fleet` | Parallel checkout; prefer `sheikh-driver` for demos unless you maintain both |

## Recording limitations (Oct 2026)

| Topic | Detail |
|-------|--------|
| **Prod API (`ENV=prod`)** | Azure sslip.io demo returned `401` / `invalid_token` (signature key mismatch) for fleet endpoints after login — use **local API on `:5082`** or fix server JWT keys before a prod-backed reel. |
| **Automated test** | `demo_walkthrough_test.dart` may exit non-zero on layout overflow; `record-ios-mobile-demo.sh` still encodes the Simulator MOV to MP4. |
| **Firebase / push** | Integration-test runs skip full Firebase init; push registration is disabled in that harness (normal on device builds). |
| **Maps** | Google Maps tiles need API keys in `Info.plist` / dart-define; simulator may show empty map without keys. |
| **Physical iPhone** | Not attached in CI agent run; use Xcode wireless debugging + LAN `API_BASE_URL` per README. |
