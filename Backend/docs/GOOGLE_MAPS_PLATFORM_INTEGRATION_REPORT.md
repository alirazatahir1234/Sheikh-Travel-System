# Google Maps Platform Integration — Final Report

Date: 2026-09-24

## 1. APIs integrated

| API | Where |
|-----|--------|
| Maps JavaScript | ERP live / replay / analytics (pre-existing migration) |
| Places API (New) | Trip / route / fuel / **customer address search** via `place-autocomplete.new.ts`; Live Map **Nearby Places** via backend `places:searchNearby` (openNow + More category) |
| Places (Legacy Nearby) | Backend reverse geocode POI (unchanged; keep enabled) |
| Geocoding | Backend reverse geocode + browser MapGeocoder where needed |
| Routes API | Frontend `gmap-routes.ts` + backend `IGoogleRoutesService` / ETA upgrade |
| Roads API | Backend snap + optional replay overlay (`showSnappedLayer`) |
| Route Optimization | Backend `IGoogleRouteOptimizationService` + `POST /api/gps/maps/optimize-tours` |
| Maps Static | Frontend URL builders + trip detail snapshot; backend URL endpoint |
| Street View Static | Live map selected vehicle + trip pickup/drop previews |
| Time Zone API | Backend `GET /api/gps/maps/timezone` + Angular client helper |

## 2. APIs configured but not currently consumed

- **Map Tiles API** — documented; fleet stays on Maps JavaScript
- Directions API — forms moved to Routes; may still be useful if legacy fallback remains

## 3. Frontend files changed (high level)

- `core/google-maps/*` — places new, routes, static URLs
- Trip / route / fuel forms — Places New + Routes polylines
- Live map — Street View for selected vehicle
- Trip detail — Street View + Static map previews
- Trip replay — optional Roads snap overlay
- `gps-tracking.service.ts` — snap + timezone clients
- Flutter: optional `Maps.local.xcconfig` gitignored (override only); committed `maps.properties` / `Maps.xcconfig` unchanged in this PR (see Security)

## 4. Backend files changed

- **No `appsettings.json` edits** (pre-existing sensitive host config lives there; touching it fails ADR-009 scans). Bind `GoogleMaps` via env / user secrets instead.
- `GoogleMapsOptions`, interfaces, `Infrastructure/Services/Google/*`
- MediatR queries/commands + dedicated `GoogleMapsController` (`api/gps/maps/*`) and `GpsFleetHealthController` (`api/gps/fleet-health`)
- ETA query prefers Routes when key present
- `GoogleMapsClientsTests` (3 passing)
- `PostConfigure` fills `GoogleMapsOptions.ServerKey` from Geocoding when the GoogleMaps section has no value (config key name stays compatible with existing secrets)

## 5. Configuration / env vars

| Env | Purpose |
|-----|---------|
| ERP / customer-hub `environment*.ts` → `googleMapsApiKey` | SheikhGo-Frontend browser key (existing committed value; scrub in follow-up PR) |
| Flutter `android/maps.properties` / `ios/Flutter/Maps.xcconfig` | Same frontend key (unchanged this PR) |
| Optional `ios/Flutter/Maps.local.xcconfig` | Local override (gitignored) |
| `GoogleMaps__ApiKey` or user secret under section `GoogleMaps` | SheikhGo-Backend (**IP-restricted / none — not HTTP referrer**) — preferred; binds to `ServerKey` |
| `Geocoding__GoogleMapsApiKey` | Legacy fallback used if the GoogleMaps section value is unset |

**Nearby Places pitfall:** a key with HTTP referrer restrictions returns `API_KEY_HTTP_REFERRER_BLOCKED` for server-side `places:searchNearby` (empty referer). Change that key to IP restrictions, or create a dedicated server key.

Example — set via user-secrets or host env (never commit the value):

```bash
# Interactive (preferred locally) — prompts for the value; do not paste secrets into docs/PRs:
dotnet user-secrets set "GoogleMaps:ApiKey" --project SheikhTravelSystem.API
# Host / Railway: set GoogleMaps__ApiKey (falls back to Geocoding__GoogleMapsApiKey)
```

## 6. SignalR / Traccar / DB

- No SignalR contract changes
- No Traccar changes
- No database migrations

## 7. Builds / tests

- `npx ng build --configuration=development` — success
- `dotnet build` API — success
- `dotnet test --filter GoogleMaps` — 3 passed

## 8. Security

- Backend key not shipped to Angular / Flutter clients
- Do not stage `Backend/SheikhTravelSystem.API/appsettings.json` for Maps work — it will fail ADR-009 secret scans
- Do not delete/replace committed frontend Maps keys in the same PR — removal hunks still fail secret scans; scrub + rotate in a dedicated follow-up
- Follow-up (separate PR): move browser keys to gitignored locals / CI injection; move appsettings sensitive values to user secrets / host env
- Restrict Backend key by IP; Frontend by HTTP referrer + Android package/SHA-1 + iOS bundle ID

## 9. Performance

- No Routes/Roads/Street View/geocode on every SignalR tick
- Routes/Time Zone cache via options TTL
- Roads min points gate + downsampled replay snap

## 10. Remaining limitations

- TrackingHub still not tenant-scoped (separate security work)
- Places Nearby still Legacy on backend
- Route Optimization requires multi-job planner UI to be useful end-to-end; API body needs Google Cloud **ProjectId**
- Create a Cloud Map ID for Advanced Markers (`DEMO_MAP_ID` fallback in local)
- Frontend key scrubbing deferred (see §8)

## 11. Production deploy steps

1. On Railway, configure `GoogleMaps__ApiKey` and/or `Geocoding__GoogleMapsApiKey` to the SheikhGo-Backend credential (host env only)
2. Restrict Backend key by IP; Frontend by HTTP referrer + Android package/SHA-1 + iOS bundle ID
3. Ensure ERP/customer-hub `environment*.ts` and Flutter maps configs have the Frontend key until the scrub PR lands
4. Restart ERP `ng serve` after key changes
