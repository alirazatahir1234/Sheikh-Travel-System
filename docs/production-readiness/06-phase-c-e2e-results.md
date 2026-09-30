# Phase C — Critical E2E dry-run results

| Field | Value |
|-------|--------|
| Date | 2026-09-30 |
| ERP | `https://www.sheikhgo.com` |
| Operator | System Admin / FLEET_MANAGER (browser session) |
| Booking | **BK-2026-1011** (`/bookings/1011`) |
| Trip | **TR-2026-1002** (`/trips/1002`) |
| Vehicle | Hyndai (LEC-8826), id `7067` |
| Driver | asghar ali, id `19` |

## Checklist

| # | Step | Result | Evidence |
|---|------|--------|----------|
| C1 | Logged into ERP | **PASS** | Session on sheikhgo.com as System Admin |
| C2 | Vehicles usable | **PASS** | Fleet: Available Hyndai + OnTrip Toyota |
| C3 | Drivers usable | **PASS** | 8 available drivers |
| C4 | Booking ready | **PASS** | Confirmed BK-2026-1011 opened |
| C5 | Assign vehicle + driver | **PASS** | Assigned Hyndai + asghar ali (Toyota rejected: not available / OnTrip) |
| C6 | Status allows start | **PASS** | Confirmed → trip Scheduled → Started |
| C7 | Start trip | **PASS** | Create Trip → TR-2026-1002; status **Started**; booking set **Started** |
| C8 | Live GPS in ERP | **PASS** | Live Map `vehicleId=50` Toyota Crolla online in Pasrur; speed/GPS good; last update seconds; SignalR realtime |
| C9 | Complete trip | **PASS** | Trip status **Completed** |
| C10 | ERP persists completed | **PASS** | Booking **Completed**; trip **Completed** after reload |

## Overall

**Operational path PASS** for assign → start → complete on production.

**GPS caveat (resolved for go-live proof):** Live tracking worked for Toyota Crolla (`vehicleId=50`) while OnTrip on BK-2026-2010. That booking was later completed in Phase D. Hyndai dry-run (TR-2026-1002) had no live GPS because the vehicle was Available / not publishing.

## Notes / follow-ups

1. UI “Apply Vehicle” on Toyota failed with API message **Vehicle is not available** (expected — OnTrip). Prefer Available + GPS-connected vehicle next time.
2. Create Trip did not auto-copy booking vehicle/driver onto the trip at first; assign-driver/assign-vehicle on the trip were required (done via API; UI has the same controls).
3. BK-2026-2010 lifecycle completed in Phase D (Arrived → Onboard → Complete); ERP status **Completed**.
4. Driver Sprint 1 minimum: **PASS** — see [`07-phase-d-driver-app-walkthrough.md`](07-phase-d-driver-app-walkthrough.md).

## Next

- Optional GPS proof on a tracker-online vehicle  
- Phase D — Driver Sprint 1 checklist  
- Do **not** start Phase 4 / DEFAULT 1 yet  
