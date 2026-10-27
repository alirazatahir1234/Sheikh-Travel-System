# Live traffic — Dubai (UAE), Pakistan, Saudi Arabia

Nexus uses **Google Routes API** with `TRAFFIC_AWARE` for real drive times. There is **no fake traffic layer** in assignment, ETA, or public tracking — only Google responses or documented straight-line fallbacks.

## Supported regions

| Code | Region | Detection |
|------|--------|-----------|
| `AE` | Dubai / UAE | Coordinate bounding box |
| `PK` | Pakistan | Coordinate bounding box |
| `SA` | Saudi Arabia | Coordinate bounding box (after AE/PK checks) |

Implementation: `MapTrafficRegions` / `GeoDistance.InferRegionCode`.

## Product surfaces

| Surface | Behavior |
|---------|----------|
| **ERP — trip form / routes** | Browser `gmap-routes.ts` → `TRAFFIC_AWARE` |
| **ERP — Smart Assignment** | Backend `GoogleRoutesService` per candidate → pickup |
| **ERP — GPS ETA** | `GET /api/gps/eta` upgrades haversine with Routes when configured |
| **Mobile — navigation** | In-app map traffic layer + planned-route / ETA refresh |
| **Public tracking** | `GET /api/public/tracking/{token}` traffic-aware ETA when Routes key is set |

Server route responses are cached (~15 minutes) on fixed origin/destination keys (`GoogleMaps:CacheMinutes`). Moving ETAs (driver position → target) use fresh origins and cache less aggressively.

## Saudi Arabia — Maps cost (research)

Google does **not** charge a separate KSA traffic surcharge. Incremental cost is **more Compute Routes Pro** events (same SKU as UAE/Pakistan: **5,000 free/month**, then **~$10 / 1,000**).

Illustrative **incremental SA-only** cost on top of a moderate PK+AE baseline (**~$34/mo** Routes Pro at 35 vehicles × 8 traffic calls/day), **FX 1 USD ≈ 276.80 PKR** (Oct 2026):

| SA vehicles | Incremental USD/mo | Incremental PKR/mo |
|-------------|------------------|---------------------|
| 10 | ~$24 | ~₨6,640 |
| 20 | ~$48 | ~₨13,290 |
| 30 | ~$72 | ~₨19,930 |

At **3 calls/vehicle/day**, divide roughly by 2.7; at **15/day**, multiply by ~1.9. Including SA is usually **worth it** at 20–50 vehicle scale if you avoid duplicate browser+server route fan-out and unbounded smart-assignment polling.

Sources: [Routes usage & billing](https://developers.google.com/maps/documentation/routes/usage-and-billing), [Global pricing](https://developers.google.com/maps/billing-and-pricing/pricing), internal research agent `d67364e0-e458-43cb-9ffa-9b77de588210`.

## Configuration

- Server: `GoogleMaps__ApiKey` (IP-restricted)
- ERP browser: `googleMapsApiKey` in environment
- Without a key, ETAs fall back to **HaversineEstimate** and UI labels say so
