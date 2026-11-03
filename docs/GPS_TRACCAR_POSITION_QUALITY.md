# Traccar position quality (operator checklist)

SheikhGo Traccar sync runs on **your hosted Traccar server** — this repo does **not** ship a Traccar container (`docker-compose.yml` is API + Redis + admin only). Apply the steps below on the Traccar host for device **19** (IMEI `862292058090598`, Jimi protocol).

## What SheikhGo already does (software)

- Skips ingest when Traccar marks `valid: false` (configurable under `GpsSettings:PositionQuality`).
- Flags **frozen coordinates** + reported movement, **LBS / zero-sat** fixes, and **implausible jumps**.
- Persists `IsGpsValid`, `GpsQualityCode` on `VehicleCurrentLocation` / `GpsPositions`.
- Raises `gps_unreliable` alerts and exposes fields on `GET /api/gps/live/fleet` and `GET /api/gps/live`.

## Traccar server — recommended processing filters

In Traccar **Server → Settings** (or `traccar.xml` / environment, depending on your install), enable position filtering so bad fixes are dropped **before** the API:

| Setting (Traccar UI / config key) | Suggested | Purpose |
|-----------------------------------|-----------|---------|
| **Filter invalid** (`filter.invalid`) | **On** | Drop positions Traccar already marks invalid |
| **Filter zero** (`filter.zero`) | **On** | Drop 0,0 coordinates |
| **Filter duplicate** (`filter.duplicate`) | **On** | Reduce repeated identical points |
| **Filter static** (`filter.static`) | On (if available) | Reduce stationary duplicate noise |
| **Filter distance** (`filter.distance`) | 10–50 m (tune) | Ignore sub-threshold jitter |
| **Filter accuracy** (`filter.accuracy`) | 50–100 m (if available) | Drop very poor accuracy |
| **Filter future** (`filter.future`) | 300–600 s | Reject far-future device timestamps |

Exact keys and UI labels vary by Traccar version — use your server’s **Position processing** section as source of truth; do not rely on undocumented REST APIs.

## Device 19 (LEB-8825) — Jimi-specific

1. Confirm **GPS antenna** connection and clear sky view (physical — software cannot fix a dead GNSS fix).
2. In Traccar device **19** → **Attributes**, note `sat` / `satellites`, `valid`, `accuracy` on live positions.
3. After **ACC wire** is installed (planned this week), ignition will align with engine state; until then SheikhGo treats null ACC as **parked at rest** per fleet rules.
4. Optional: tighten report intervals only after fix quality improves (see `Backend/docs/GPS_LIVE_TRACKING.md`).

## Verify

1. Traccar web UI → device 19 → latest position: `valid`, `sat`, speed vs map.
2. SheikhGo `GET /api/gps/live/fleet` → vehicle **2**: `isGpsValid`, `gpsQualityCode`.
3. Alerts feed → `gps_unreliable` when coordinates freeze with movement speed.

## Requires physical work (not software)

- Restoring a real GNSS fix (antenna, placement, device hardware).
- ACC wiring for trustworthy ignition / parked vs idle.
- Moving the vehicle under open sky to re-acquire satellites.

Software mitigations **do not** invent the true Basrur/Tamour coordinates — they flag stale anchors and stop updating the live map with bogus speed.
