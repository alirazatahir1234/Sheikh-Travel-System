# Smart trip assignment

Weighted driver matching when a trip has no manual driver assignment.

## Signals

| Factor | Source |
|--------|--------|
| Driver rating | `Drivers.Rating` |
| Proximity | `VehicleCurrentLocation` via active `AssignmentHistory` |
| Fatigue | `DriverAttendance` hours today |
| Fuel | `VehicleCurrentLocation.FuelLevel` (OBD when available) |
| Delivery / on-time pickup | Planned start vs ETA |
| Traffic / route | Google Routes (`TRAFFIC_AWARE`) when `GoogleMaps:ApiKey` is configured — UAE, Pakistan, and Saudi Arabia (`MapTrafficRegions`) |

When Routes API is unavailable, distance and ETA fall back to haversine + average speed; `TrafficSource` is stored as `HaversineEstimate`.

## API

- `GET /api/trips/{id}/smart-assignment/suggestions`
- `POST /api/trips/{id}/smart-assignment/auto`
- `POST /api/trips/{id}/smart-assignment/override`
- `GET /api/trips/{id}/smart-assignment/decisions`
- `GET /api/trips/smart-assignment/recent`

## Driver reject flow

If the driver rejects a trip that was offered through smart assignment (`TripSmartAssignmentDecisions`), the trip driver is cleared and the next ranked driver is assigned automatically. Otherwise the legacy cancel transition applies.

## Persistence

`TripSmartAssignmentDecisions` stores score breakdown JSON for admin explainability.
