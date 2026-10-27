# SheikhGo Nexus — demo walkthrough script

Use this if you prefer **live narration** (QuickTime, OBS, or Loom) over the automated recording.

**Target length:** 6–8 minutes · **Resolution:** 1920×1080 · **App:** `http://localhost:4200` or `https://www.sheikhgo.com`

**Demo login:** `admin@sheikhtravel.com` / `Pass@123` (local demo tenant only — do not share publicly).

---

## 0:00 — Title card (5 s)

**On screen:** Logo / “SheikhGo NEXUS — Fleet intelligence platform”

**Say:** “This is SheikhGo Nexus — one stack for live GPS, fleet operations, and tool-first AI.”

---

## 0:05 — Home hero & NEXUS centerpiece (1:15)

1. Open `/` (signed out).
2. Pause on hero: tagline “GPS, Nexus, and AI in one premium operations stack.”
3. Scroll to **#see-the-platform** — fleet intelligence dashboard image and callouts (Live GPS, AI, Geofencing, GPS Alerts).
4. **Say:** “The home page is redesigned around NEXUS — not a generic SaaS template. The centerpiece is the operational dashboard your dispatchers actually use.”

---

## 1:20 — 14 module gallery (1:00)

1. Scroll to **#platform-modules**.
2. Slowly scroll the grid — all 14 module screenshots (Dashboard through Notifications).
3. **Say:** “Every tile is a real capture from the demo workspace — live tracking, alerts, geofences, trips, vehicles, drivers, fuel, maintenance, reports, AI, customers, and notifications.”

---

## 2:20 — AI Solutions (0:45)

1. Navigate to `/ai-solutions`.
2. Scroll features — assistants, tools, transparency copy.
3. **Say:** “AI Solutions explains how assistants connect to your operational tools — fleet Q&A, alerts, and workflows — without overclaiming black-box models.”

---

## 3:05 — Live Tracking & GPS Alerts (marketing) (1:00)

1. `/live-tracking` — scroll hero and capability sections.
2. `/gps-alerts` — speed, health, safety scenes.
3. **Say:** “Marketing pages mirror what ships: live map storytelling, speed and device alerts, and fleet health — aligned with the ERP modules.”

---

## 4:05 — Sign in & live ERP (2:30)

1. `/auth/login` → sign in.
2. **Dashboard** (`/dashboard`) — 20 s overview.
3. **Live map** (`/gps-tracking/live`) — zoom/pan if map loads; mention speed and status.
4. **GPS Alerts** (`/gps-tracking/alerts`) — filter or open one row.
5. **Bookings** (`/bookings`) — list or calendar view if module enabled.
6. **AI** (`/ai`) — one example prompt if UI allows.
7. **WhatsApp** (`/whatsapp`) — inbox or settings snippet.
8. **Reports** (`/reports`) — one report type.
9. **Say:** “From here you’re in the same Nexus workspace your team uses daily — bookings, WhatsApp, and finance-style reporting roll up under operations and reports depending on tenant modules.”

---

## 6:35 — Request demo CTA (0:25)

1. Sign out or open guest `/request-demo`.
2. **Say:** “When you’re ready, request a demo — we’ll walk your fleet workflow on your data model.”

---

## 7:00 — Close (5 s)

**On screen:** `sheikhgo.com` · “Web ERP · Mobile fleet · Shared API”

---

## Recording tips

| Tool | Command / action |
|------|------------------|
| Automated MP4 | From `Frontend/sheikhgo-erp`: `node tools/record-nexus-demo.mjs` |
| Refresh module PNGs | `node tools/capture-nexus-modules.mjs` |
| macOS QuickTime | File → New Screen Recording → select window 1920×1080 |
| Share | Upload MP4 to Drive/YouTube; embed from `public/website/demo/` on deploy |

## Covered vs optional

| Covered in script | Often skipped in short cut |
|-------------------|----------------------------|
| Home + NEXUS centerpiece | `/services`, `/about`, `/contact` |
| 14-module gallery | Individual geofence editor deep-dive |
| AI Solutions marketing | Mobile Flutter app |
| Live tracking + alerts (web + ERP) | Every ERP sub-route (fuel, maintenance one-by-one) |
| Bookings, WhatsApp, Reports | Dedicated finance module (route not in shell) |
