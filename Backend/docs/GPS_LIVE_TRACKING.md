# GPS Live Tracking (Event-Driven Foundation)

SheikhGo live GPS is an **ingest-once, push-many** pipeline: each device fix is written to history + current location, then pushed over SignalR. Clients treat HTTP poll as **fallback only** when the hub is down. UI refresh timers must never drive DB writes.

## Pipeline

```text
Jimi VG03 / phone GPS
        │
        ▼
   Traccar (device reports)
        │  Adaptive HTTP pull (5s → 5 min)
        ▼
TraccarSyncOrchestrator → IngestPositionCommand
        │                    ▲
        │                    │  POST ingest / driver-app location
        ├─ GpsPositions (INSERT history)
        ├─ VehicleCurrentLocation (MERGE latest)
        └─ LocationBroadcastService → TrackingHub
                    │
        ┌───────────┴───────────┐
        ▼                       ▼
  ERP Live Map            Flutter live map
  (SignalR primary)       (ReceiveLocationUpdate)
        │                       │
        └─ HTTP poll only if hub disconnected
```

**Storage rule:** every ingested fix → history; only latest → `VehicleCurrentLocation`; clients receive push of latest. Never persist UI refresh ticks.

## Recommended Jimi VG03 report intervals

Configure on the device (SMS / vendor portal). These are **documented targets**, not automated provisioning.

| Vehicle state | Report interval |
|---------------|-----------------|
| Moving | **5–10 s** |
| Idle (ignition ON, speed ≈ 0) | **30–60 s** |
| Ignition OFF / parked | **5–15 min** |
| SOS / panic | **Immediate** |

Phone GPS (driver app) already posts adaptively (~8 s moving / ~25 s idle) via HTTP ingest; it is a producer, not a Traccar consumer.

## Adaptive Traccar → SheikhGo sync

With `Traccar:AdaptivePositionSync` = `true` (default), after each position sync the orchestrator picks the next delay from the fleet snapshot:

| Fleet signal | Next position sync | Config key (default) |
|--------------|--------------------|----------------------|
| Any vehicle speed ≥ `MovingSpeedKmh` (10) | Moving | `MovingIntervalSeconds` (**5**) |
| 0–10 km/h, ignition ON | Slow traffic | `SlowTrafficIntervalSeconds` (**15**) |
| Ignition **null** and speed &gt; `UnknownIgnitionMovingSpeedKmh` (2) | Moving | `MovingIntervalSeconds` (**5**) |
| Ignition ON at rest, **or ignition null at rest** | Idle | `IdleIntervalSeconds` (**30**) |
| Ignition **explicitly OFF** (whole sample) / empty fleet | Parked | `ParkedIntervalSeconds` (**30**) |
| SOS / alarm on sample | ASAP (moving floor) | uses `MovingIntervalSeconds` |

`Ignition == null` must **never** force the Parked multi-minute cadence — VG03 often omits ignition.

`PositionSyncIntervalSeconds` is the **moving floor** (default **5**), not a static 30 s cadence. FixTime dedup still prevents duplicate history rows when parked sync is slow.

Ops: Traccar sync-status exposes the effective adaptive interval and reason (also reflected in ops metrics).

Key settings live under `Traccar` in `appsettings.json` (`DeviceSyncIntervalSeconds`, adaptive knobs above).

## Client policy

| Client | Connected | Disconnected |
|--------|-----------|--------------|
| ERP Live Map / vehicle GPS panel | SignalR only (`ReceiveLocationUpdate`) | HTTP poll ~5–10 s until reconnect |
| Flutter driver live map | Join `vehicle_{id}`; subscribe `ReceiveLocationUpdate` | HTTP GET `/Vehicles/{id}/gps` for that vehicle only |
| Flutter fleet dispatcher map | Join dispatcher group; same hub event | Existing fleet poll behavior |

Outbound driver location remains HTTP → ingest. Do not invent broker queues for this path.

## Explicit non-goals (this foundation)

- RabbitMQ / Kafka
- Traccar WebSocket client or position-forward webhook (**Phase 2**)
- Automated Jimi SMS / device provisioning
- Rewriting trip / geofence / overspeed engines

## Phase 2 (when needed)

1. Traccar position forward / webhook **or** a Traccar WebSocket client so SheikhGo is push-fed instead of pull-adaptive.
2. Message broker only at very large scale (order of **10k+** vehicles), not as a default for live map freshness.

## Verification checklist

- Moving vehicle: sync status ~5–10 s; ERP map updates via SignalR without REST spam.
- Parked fleet: sync backs off to minutes.
- Disconnect SignalR in ERP → fallback poll; reconnect → push resumes.
- Ingest still writes `GpsPositions` + updates `VehicleCurrentLocation` once per fix.
- SOS still pushes `ReceiveSosAlert` immediately on ingest.

## Related code

- Sync: `TraccarSyncService`, `TraccarSyncOrchestrator`, `TraccarOptions`, `TraccarAdaptiveInterval`
- Ingest / push: `IngestPositionCommand`, `LocationBroadcastService`, `TrackingHub`
- ERP: `gps-realtime.service.ts`, live-map + vehicle profile GPS panels
- Flutter: `signalr_service.dart`, `live_map_screen.dart`, `fleet_realtime_service.dart`
