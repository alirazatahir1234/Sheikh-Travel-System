# Phase D — Driver App Sprint 1 walkthrough

| Field | Value |
|-------|--------|
| Status | **PASS** (Sprint 1 minimum) |
| Date | 2026-09-30 |
| Prerequisite | Phase B PASS · Phase C PASS (incl. Live GPS) |
| App | `Frontend/sheikh-driver` |
| API | `https://sheikh-travel-system-production.up.railway.app/api` |
| Device | Android emulator (`emulator-5554`) + driver API lifecycle |

## Goal (minimum for go-live)

```text
Driver login
  → See assigned trip
  → Accept / Start (lifecycle)
  → GPS updates (batch/location)
  → Complete trip
  → ERP shows completed
```

## Sprint 1 checklist (this pass)

| # | Step | Pass? |
|---|------|-------|
| D1 | App launches against **production** API | [x] Railway via `--dart-define` |
| D2 | Driver login (phone + password) | [x] Imran / 03021234567 |
| D3 | Dashboard loads; assigned trip visible | [x] BK-2026-2010 on Trip Details |
| D4 | Trip detail opens | [x] Status **Driving to pickup**; Hina Ali; Toyota Crolla |
| D5 | Accept / Start (or Arrived → Onboard as applicable) | [x] `POST …/arrived` → AtPickup; `POST …/onboard` → Enroute |
| D6 | Continue Trip opens maps (if button present) | [x] Navigate CTA + `ExternalMapsLauncher` (Google / Waze); emulator deep-link smoke optional |
| D7 | Location / GPS posting works (no hard error) | [x] Covered by Phase C Live Map on Toyota / BK-2026-2010 |
| D8 | Complete trip | [x] `POST …/complete` → Completed (tripId **2**) |
| D9 | ERP booking/trip reflects completion | [x] Booking **BK-2026-2010** status **Completed**; dropoffTime set |

Skip for later: offline replay, iOS matrix, payments Sprint 2, store tracks.

## Lifecycle evidence (API)

Driver: Imran Yousaf (id 1) · Trip id **2** · Booking **BK-2026-2010**

| Action | HTTP | Result |
|--------|------|--------|
| Arrived | 200 | Trip updated to **Arrived at pickup** |
| Onboard | 200 | Trip updated to **Enroute** |
| Complete | 200 | Trip updated to **Completed** |

ERP verify (`GET /api/bookings/2010` as `admin@sheikhtravel.com`): `status: "Completed"`.

Post-complete: driver `/driver-app/trips` returns `[]` (active list cleared); `driverStatus: Available`.

## How to run (Android emulator)

From repo root:

```bash
cd Frontend/sheikh-driver
flutter pub get
flutter run -d emulator-5554 \
  --dart-define=ENV=prod \
  --dart-define=API_BASE_URL=https://sheikh-travel-system-production.up.railway.app/api
```

Or: `./scripts/run_prod_device.sh` if present.

## Login (this dry-run)

| Field | Value |
|-------|--------|
| Driver | **Imran Yousaf** (id 1) — BK-2026-2010 + Toyota live GPS |
| Phone | `03021234567` |
| Password | Same lab password used for ERP driver accounts (set in app; do not commit) |

## Fail → stop

Login 401, empty trips with assignment present, start/complete API errors, or ERP not updating → P0; capture message before Phase 4.
