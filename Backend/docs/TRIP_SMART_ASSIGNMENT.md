# Smart Trip Assignment

Constraint-based + weighted driver matching. Product name: **Smart Trip Assignment** (not LLM dispatch). Backend target: **net10.0**.

## Policy (Ali defaults)

| Setting | Value |
|---------|--------|
| Mode | **SuggestOnly** — staff confirm required |
| Create-time auto-assign | **Disabled** |
| AutoApply | Off until ~2 weeks shadow observation |
| OnTrip drivers | Hard exclude |
| No linked vehicle | Hard exclude |
| Maintenance (status or critical WO) | Hard block |
| EV range buffer | **15%** (`× 1.15`) |
| Max hours worked (hard) | **12 h** |
| LLM explain | Not used |

## Hard filters (before scoring)

Eligibility / docs · OnTrip · no vehicle · capacity · required vehicle type (when set) · maintenance · shift limit · schedule conflict · EV range.

Excluded candidates are returned on suggestions with reason counts — they never appear in the ranked list.

## Soft scoring signals (weights sum = 1.0)

| Factor | Weight | Source |
|--------|--------|--------|
| Driver rating | 0.18 | `Drivers.Rating` |
| Proximity | 0.15 | GPS / Google Routes |
| Fatigue | 0.12 | `DriverAttendance` hours today |
| Fuel / EV battery | 0.12 | `VehicleCurrentLocation.FuelLevel` |
| On-time pickup | 0.12 | Planned start vs ETA |
| Route / traffic | 0.12 | Google Routes or Haversine |
| Performance / safety | 0.10 | Latest `DriverPerformanceScoreHistory` |
| Cost / efficiency | 0.05 | `Vehicles.FuelAverage` (EV boosted) |
| Utilization balance | 0.04 | Recent 7d odometer span from fuel logs |

## API

- `GET /api/trips/{id}/smart-assignment/suggestions` — ranked + excluded + mode
- `POST /api/trips/{id}/smart-assignment/confirm` — staff confirm (primary apply path)
- `POST /api/trips/{id}/smart-assignment/override` — admin override + reason
- `POST /api/trips/{id}/smart-assignment/auto` — blocked while SuggestOnly
- `GET /api/trips/{id}/smart-assignment/decisions`
- `GET /api/trips/smart-assignment/recent`

## UI

ERP: `/trips/smart-assignment` — factor bars, Confirm recommendation, excluded panel, Suggest only badge.

## Driver reject flow

If the driver rejects a trip that was offered through smart assignment, the trip driver is cleared and the next ranked eligible driver is re-offered (operational continuity).

## Persistence

`TripSmartAssignmentDecisions` stores score breakdown JSON. Outcomes include `Confirmed`, `Proposed`, `Overridden`, `Rejected`, `Superseded`, `Shadow`.
