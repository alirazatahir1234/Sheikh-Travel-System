# Phase C — Critical E2E dry-run checklist

| Field | Value |
|-------|--------|
| Status | IN PROGRESS |
| Prerequisite | Phase B PASS ([`05-security-verification-results.md`](05-security-verification-results.md)) |
| ERP | `https://www.sheikhgo.com` |
| Tenant | Sheikh Travel (id 1) — data-rich production tenant |
| Goal | One trip completes without developer hotfix / SQL |

## Flow

```text
ERP login
  → Vehicle usable
  → Driver usable
  → Create booking/trip
  → Assign vehicle + driver
  → Driver starts (app or ERP action)
  → Live GPS visible
  → Complete trip
  → ERP shows completed
```

## Checklist

| # | Step | How | Pass? |
|---|------|-----|-------|
| C1 | Logged into ERP as tenant operator | Browser session on sheikhgo.com | [x] |
| C2 | Open Vehicles — at least one active vehicle | Vehicles module | [x] |
| C3 | Open Drivers — at least one active driver | Drivers module | [x] |
| C4 | Create booking (or open existing assignable booking) | Bookings → BK-2026-1011 | [x] |
| C5 | Assign vehicle + driver | Hyndai + asghar ali | [x] |
| C6 | Trip/booking status allows start | Confirmed → Started | [x] |
| C7 | Start trip (Driver app **or** ops/start action if available) | TR-2026-1002 Started | [x] |
| C8 | Live tracking / GPS visible in ERP | **PASS** — Live Map shows Toyota Crolla LEC-8825 online @ Pasrur, 7 km/h, last update ~seconds, SignalR live (`/gps-tracking/live?vehicleId=50`) | [x] |
| C9 | Complete trip | Trip Completed | [x] |
| C10 | ERP reflects completed + persisted after refresh | Booking + trip Completed | [x] |

## Pass criteria

- [x] C1–C7, C9–C10 checked (C8 partial — see results doc)
- [x] No manual SQL or emergency deploy required

See [`06-phase-c-e2e-results.md`](06-phase-c-e2e-results.md).

## Fail → stop

If C5/C7/C8/C9 fail: treat as **P0 go-live blocker**. Capture screen + API error; do not start Phase 4 / DEFAULT 1.

## Out of scope this pass

Driver Sprint 1 full matrix (both OS), payments Sprint 2, Phase 4, DEFAULT 1 removal.
