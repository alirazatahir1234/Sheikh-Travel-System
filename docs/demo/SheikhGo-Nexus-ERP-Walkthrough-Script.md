# SheikhGo Nexus ERP — application walkthrough script

Automated recording: `Frontend/sheikhgo-erp/tools/record-nexus-erp-demo.mjs`  
**Target length:** ~3 minutes · **1920×1080** · **Logged-in ERP only** (not marketing pages)

**Demo login (local):** `admin@sheikhtravel.com` / `Pass@123`

**Output:** `Frontend/sheikhgo-erp/public/website/demo/sheikhgo-nexus-erp-walkthrough.mp4`

**Audio:** Mixkit “Motivating Mornings” (#33) — see `docs/demo/README.md`

---

## Value proposition (title card · ~3 s)

**On screen:** SheikhGo NEXUS — *One workspace for live GPS, fleet ops, and driver payouts*

**Caption / VO:** “SheikhGo Nexus unifies tracking, alerts, operations, and payment wallets—so dispatch, finance, and drivers stay on one platform.”

---

## 0:03 — Sign-in & operations shell (~12 s)

1. `/auth/login` → dashboard.
2. Expand sidebar; scroll module rail.
3. **Caption:** Operations dashboard

---

## 0:15 — Priority modules (10–15 s each)

| Module | Route | Caption |
|--------|-------|---------|
| **Live tracking** | `/gps-tracking/live` | Live GPS tracking |
| **Alerts** | `/gps-tracking/alerts` | Speed & GPS alerts |
| **Payment wallet** | `/driver-wallets` | Driver payment wallet |

Pause on map load, alert rows, and wallet balances. Light scroll on live map.

---

## 0:57 — FAQ caption 1 (~4 s)

**Q:** How does Nexus connect to existing GPS systems?  
**A:** Device feeds (e.g. Traccar-compatible) ingest into the GPS engine; Nexus surfaces live map, history, and alerts without replacing your hardware vendor.

---

## 1:01 — Fleet & operations (~12 s per module)

| Route | Caption |
|-------|---------|
| `/gps-tracking/geofences` | Geofencing |
| `/vehicles` | Vehicles |
| `/bookings` | Bookings |
| `/fleet/maintenance` | Maintenance |

---

## 1:49 — Illustrative case study (~4 s)

**On screen:** *Illustrative fleet example — not a named customer*

- **−18%** idle fuel spend (route + idle alerts)  
- **−12 hrs/week** manual dispatch reconciliation  
- **+24%** on-time cash-out approvals via driver wallet  

---

## 1:53 — Finance & intelligence (~12 s per module)

| Route | Caption |
|-------|---------|
| `/payments` | Payments |
| `/reports` | Reports & analytics |
| `/whatsapp` | WhatsApp inbox |
| `/ai` | AI assistant |

---

## 2:41 — FAQ caption 2 (~4 s)

**Q:** How secure is the data?  
**A:** Tenant-scoped access, role-based permissions, encrypted transport, and audit-friendly workflows—production deployments follow your compliance review.

---

## 2:45 — Driver app (~4 s)

**On screen:** Phone mock + SheikhGo Driver app (trips, wallet, notifications).  
Use Simulator capture when available; otherwise product/driver artwork from `/website/product/drivers.png`.

---

## 2:49 — Book a Demo CTA (~5 s)

1. Guest view: `/request-demo` (form hero).
2. **Caption:** Book a demo

**Close card:** `sheikhgo.com` · *Book a Demo · Start your fleet review*

---

## Recording notes

- **Per-module dwell:** 10–15 s (`shotMs` in recorder); priority modules at upper range.
- **Captions:** Bottom overlay during ERP scenes; FAQ / case study / driver as full-screen cards.
- **Regenerate:** `cd Frontend/sheikhgo-erp && node tools/record-nexus-erp-demo.mjs`
- **Stitch + BGM only:** `STITCH_ONLY=1 node tools/record-nexus-erp-demo.mjs`
