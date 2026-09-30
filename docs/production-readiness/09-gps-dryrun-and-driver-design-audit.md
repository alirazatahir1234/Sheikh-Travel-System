# GPS dry-run + Driver App design audit — 2026-09-30

## GPS-online dry-run — PASS

| Step | Result |
|------|--------|
| Vehicle | Toyota Crolla **id 50** — `gpsOnline: true` (Pasrur) |
| Booking | **BK-2026-3010** (id 3010) Confirmed |
| Assign | Vehicle 50 + Driver Imran (1) |
| Trip | **TR-2026-2002** (id 2002) Started |
| Live GPS during trip | `/gps/live` → vehicle 50, `bookingId: 3010`, lat/lng/speed updating |
| Driver lifecycle | Arrived → Onboard → Complete |
| ERP final | Booking **Completed** · Trip **Completed** |

Evidence: production API, 2026-09-30.

---

## Driver App design findings (from emulator screenshots)

### P0 / must fix for driver UX

1. **Trip Details shows multiple primary actions at once**  
   Reject + Navigate + Arrived + Onboard all stacked as equal CTAs while status is “Driving to pickup”. Drivers should see **one** next step.  
   **Fix applied:** sequential primary CTA (Accept → Arrived → Onboard → Complete); Navigate/Reject secondary.

2. **Stale completed trip still listed as active**  
   My Trips / Trip Details still showed **BK-2026-2010** as “Driving to pickup” after it was completed via API. Likely offline cache / no refresh after external complete.  
   **Fix applied:** online fetch always replaces cache (incl. empty); offline cache filters out completed/cancelled.

### P1 — product / layout

3. **“Driver view” dashboard shows fleet manager content**  
   Subtitle says Driver view, but body shows fleet KPIs (Drivers / Present / Absent / Lic. expired), universal search (“Search vehicle, driver…”), Fleet Status Overview, Critical Alerts.  
   Expected driver home: my vehicle, today’s trips, earnings — not whole-fleet attendance.  
   **Fix applied:** layout keyed off `session.isDriverOnly` / `AuthMode.driver`; `dashboardProvider` watches `fleetSessionProvider`; login invalidates dashboard + trips; stale fleet payload triggers refetch.

4. **Dual staff/driver shell in one app**  
   Bottom nav mixes driver trips with fleet Tracking/Inbox. Confusing for field drivers. Consider hard driver-only shell when `authMode == driver`.

### P2 — polish

5. Vehicle name typo **“Crolla”** is master data, not UI.  
6. Drop-off time `-` when unknown — show “ETA pending” or hide.  
7. Inbox badge `7` with no triage on driver home.

---

## Code changes this pass

- `Frontend/sheikh-driver/lib/features/trips/presentation/trip_detail_screen.dart` — sequential CTAs  
- `Frontend/sheikh-driver/lib/features/trips/data/trips_api.dart` — cache replace + offline filter  
- `Frontend/sheikh-driver/lib/features/dashboard/presentation/dashboard_screen.dart` — force driver layout from session  
- `Frontend/sheikh-driver/lib/features/dashboard/presentation/dashboard_notifier.dart` — watch auth session  
- `Frontend/sheikh-driver/lib/features/trips/presentation/trips_notifier.dart` — watch auth session  
- `Frontend/sheikh-driver/lib/features/auth/presentation/login_screen.dart` — invalidate dashboard/trips on login  

Hot restart the emulator app to pick up UI changes, then pull-to-refresh Trips / Dashboard.

## Remaining design work

- [x] Enforce true driver dashboard when `session.isDriverOnly`
- [x] Invalidate `dashboardProvider` / `tripsProvider` on login
- [ ] Optional: completed-trips history tab (separate from active My Trips)
- [ ] Optional: hard driver-only bottom nav when `authMode == driver`
