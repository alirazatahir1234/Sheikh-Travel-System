# Road Speed Alerts (`ROAD_SPEED_ALERTS`)

Company-toggleable overspeed alerts based on the **posted road speed limit** at the vehicle’s GPS position (Google Roads Speed Limits), not a fixed Traccar/device threshold.

## Behaviour

1. On every GPS ingest (`IngestPositionAsync` — Traccar sync, driver app, `POST /api/gps/positions`), after fixed-rule overspeed evaluation.
2. If tenant module **`ROAD_SPEED_ALERTS`** is enabled and `RoadSpeedAlerts:ApiEnabled` is true:
   - Resolve posted limit (cache → Google Roads `speedLimits`).
   - Alert when `currentSpeedKmh >= roadLimitKmh + OverspeedMarginKmh` (default margin **20**).
3. Persist `GpsAlertEvents` with `EventType = road_speed_exceeded`, notify via `NotificationDecisionEngine` (template `speed_alert`), optional telematics violation.
4. Dedup: at most one alert per vehicle per `AlertDedupMinutes` (default 5) while continuously overspeed.

## Module toggle / premium subscription add-on (same pattern as AI)

| Item | Value |
|------|--------|
| Module code | `ROAD_SPEED_ALERTS` |
| Legacy key | `road-speed-alerts` |
| Commercial | **Premium paid add-on** (`SubscriptionAddOnCatalog`) — not in Starter/Pro/Enterprise plan module packs |
| Price | `MonthlyAmount` null / config placeholder (no invented list price; same as AI add-on) |
| Default for new tenants | **Off** |
| Dependencies | `ALERTS` → `GPS` → `FLEET` → `DASHBOARD` |
| Subscribe / enable | Platform → Subscription Management (add-ons) · Module Management · Company Profile → Modules |
| API | `PUT /api/platform/tenants/{id}/modules` with `"ROAD_SPEED_ALERTS"` in `moduleCodes` |
| Runtime gate | `ITenantModuleService.IsModuleEnabledAsync(tenantId, "ROAD_SPEED_ALERTS")` inside `RoadSpeedAlertEvaluator` |

## Google APIs used

| API | Endpoint | Purpose |
|-----|----------|---------|
| **Roads API — Speed Limits** | `GET https://roads.googleapis.com/v1/speedLimits?path=lat,lng&units=KPH` | Posted limit + placeId |
| Roads snapToRoads (existing) | `maps.googleapis.com/.../snapToRoads` | Display / replay only — **not** used for limits |

### Google Cloud setup (Ali)

1. Create/use a **server** Maps key (IP-restricted). Do **not** commit it.
2. Enable **Roads API** on the Google Cloud project.
3. Confirm **Speed Limits** product access — Google often restricts this to approved accounts. If the API returns 403/404, alerts simply do not fire (no invented limits).
4. Set secret:

```bash
dotnet user-secrets set "GoogleMaps:ApiKey" "<sheikhgo-backend-maps-key>"
# or: GoogleMaps__ApiKey / Geocoding__GoogleMapsApiKey
```

5. Optional tunables (see `docs/local-dev-config.example.json` → `RoadSpeedAlerts`).

## Caching / cost controls

| Rule | Default | Effect |
|------|---------|--------|
| Module disabled | — | **Zero** Roads API calls |
| `ApiEnabled: false` | false kill-switch | No API / no alerts |
| `MinSpeedToEvaluateKmh` | 30 | Skip evaluation when speed cannot breach limit+margin for typical roads |
| Process-local last limit | per vehicle | If movement &lt; `MinMovementMeters` (75 m), reuse last limit — no lookup |
| Memory cache | grid / placeId | Hot path within process |
| SQL `RoadSpeedLimitCache` | TTL 24 h | Durable across restarts; shared geographically (not tenant-scoped) |
| Grid cell | ~0.0009° (~100 m) | Cache key when placeId unknown |
| Circuit breaker | 5 failures → 15 min open | Stops hammering Google on outages |
| Alert dedup | 5 min | Notification spam control (also engine cooldown 15 min) |

### Rough cost estimate

Assume 50 vehicles, 1 fix / 10 s while moving, 8 h/day:

- Naïve uncached: ~144k Roads calls/day.
- With 75 m movement gate + 24 h grid/place cache: typically **tens to low hundreds** of unique segment lookups/day for a regional fleet (most fixes hit memory/SQL). Exact volume depends on corridor diversity.

## Separate from existing overspeed

| Mechanism | Event type | Limit source |
|-----------|------------|--------------|
| `GpsAlertRules.SpeedLimitKmh` | `speed_exceeded` | Fixed company/vehicle rule (default 100) |
| Traccar `deviceOverspeed` | `deviceoverspeed` | Device/server threshold |
| **This module** | `road_speed_exceeded` | Posted road limit + margin |

## Key files

- `TenantModuleCatalog.cs` / `ModuleRegistrySeed.cs` / `SubscriptionAddOnCatalog.cs`
- `ModuleRoadSpeedAlertsActiveMigration.cs` / `ModuleRoadSpeedAlertsPremiumAddOnMigration.cs` / `RoadSpeedLimitCacheMigration.cs`
- `RoadSpeedAlertOptions.cs` / `RoadSpeedAlertEvaluator.cs` / `RoadSpeedLimitLookupService.cs`
- `GoogleRoadsService.GetSpeedLimitAsync`
- Hook: `GpsTrackingRepository.IngestPositionAsync`
