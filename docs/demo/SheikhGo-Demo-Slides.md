# SheikhGo Demo Slides — Outline

Copy each slide into Google Slides / PowerPoint. Pair with the interactive deck:

`docs/demo/SheikhGo-Demo-Slides.html` (open in a browser; ← → keys to navigate).

Suggested length: **14 slides · ~20 minutes** live demo.

---

## Slide 1 — Title

**SheikhGo**  
Fleet operations, from office to road

- Web ERP + Mobile Fleet App + Shared API  
- Tagline: *One platform for dispatch, compliance, and live fleet control*

---

## Slide 2 — Agenda

1. Problem  
2. Platform overview (Web + Mobile)  
3. Live walkthrough — Web  
4. Live walkthrough — Mobile  
5. Security, roles & audit  
6. Next steps  

---

## Slide 3 — The problem

| Typical today | With SheikhGo |
|---------------|---------------|
| WhatsApp / calls | Structured dispatch |
| Spreadsheets | Vehicles & drivers in ERP |
| No office GPS | Live map + alerts |
| Paper fuel / inspections | Digital evidence |
| Weak change history | Audit Center |

---

## Slide 4 — Two surfaces · one system

**Web (sheikhgo-erp)** — Command center  
Companies · users/roles · bookings · fleet/GPS · modules · Security & Audit

**Mobile (SheikhGo Fleet)** — Field execution  
Trips · navigation · GPS · attendance · fuel · inspections · SOS · offline

---

## Slide 5 — Architecture

`ERP (Angular) + Fleet App (Flutter) → HTTPS API → SQL + SignalR`

Pillars: tenant isolation · permission engine · real-time hubs

---

## Slide 6 — Web Part 1: Platform admin

Demo path:

1. Login (Super Admin / Tenant Admin)  
2. Platform hub  
3. Companies → organization  
4. Access Control (users / roles)  
5. Modules · Features · Subscriptions  

---

## Slide 7 — Web Part 2: Operations

Show briefly:

- Bookings (create / assign)  
- Fleet live map & GPS alerts  
- Trips lifecycle  
- Maintenance / compliance  
- Payments  

---

## Slide 8 — Web Part 3: Security & Audit

- **Security Center** — password age, lockout, session idle, audit level  
- **Audit Center** — searchable events, retention, CSV export  

---

## Slide 9 — Mobile Part 1: Fleet app

Demo path:

1. Driver / staff login  
2. Role dashboard  
3. Open assigned trip  
4. Navigation + live GPS  
5. Attendance check-in  

Talking points: same production API · internet-only · EN/AR  

---

## Slide 10 — Mobile Part 2: Field workflows

Fuel photo · Inspections · Documents · Alerts · SOS · Offline sync  

---

## Slide 11 — Who uses what

| Web-first | Mobile-first |
|-----------|--------------|
| Super Admin | Driver |
| Tenant Admin | Driver Manager |
| Fleet / Ops Manager | Staff / Ops on mobile |
| Dispatcher | |

---

## Slide 12 — 20-minute script

| Time | Focus |
|------|--------|
| 2 min | Problem & value |
| 7 min | Web: Platform → Booking → Live map |
| 7 min | Mobile: Trip → GPS → Fuel/Attendance |
| 3 min | Security / Audit |
| 1 min | Close & Q&A |

---

## Slide 13 — Demo readiness checklist

**Web:** stable env · demo tenant · admin logins · sample booking  

**Mobile:** release APK/IPA · Production API · linked driver user · location on · hotspot backup  

---

## Slide 14 — Close

**Questions?**  
*Run the office on the web. Run the road on mobile. One SheikhGo platform.*

Next step: pilot company · train dispatch · enroll drivers  

---

## Presenter notes (optional)

- Prefer Production or a frozen staging DB — avoid live migrations mid-demo.  
- Pre-assign one trip to the demo driver so mobile opens with content.  
- If SignalR drops, fall back to pull-to-refresh on map / trips.  
- Keep Mac LAN / local API out of the story for external demos — use Production HTTPS builds.
