# Fatigue detection (alert framework + prototypes)

## Scope

- **In scope:** Signal ingest (dashcam webhook, driver phone prototype, OBD supporting data), driver ack window, escalation to GPS commands, ERP admin views, driver mobile prompt, marketing copy.
- **Out of scope for production AI:** Full specialized in-cab camera model — see reminder for Ali at end of rollout notes.

## Signal sources

| Source | API | Notes |
|--------|-----|--------|
| Dashcam / external AI | `POST /api/fatigue-detection/ingest` | JWT + `Gps.AlertResolve`; pass `source: dashcam` |
| Driver phone (ML Kit prototype) | `POST /api/driver-app/fatigue/signal` | Driver role; `source: mobile_camera_prototype` |
| OBD-II supporting | Same ingest with `obdSpeedKmh`, `obdHardBrakingEvents15Min`, `obdProlongedHighSpeed` | Does not replace camera signal; nudges composite risk |

## Response pipeline

1. Ingest creates/updates `DriverFatigueEvents` with ack deadline (tenant settings).
2. Admin/dispatch notified (`driver_fatigue` / `driver_fatigue_obd`).
3. Driver push + in-app prompt (`driver_fatigue_prompt`).
4. `FatigueEscalationHostedService`: ack timeout → buzzer → buzzer → `engineStop` (if auto-stop enabled).
5. Commands queued via `IGpsAutomatedCommandService` → `GpsDeviceCommands` → Traccar/SMS transport.

**Gradual slowdown:** Not all trackers support speed-limited cruise; escalation documents intent and uses buzzer then engine cut/relay where configured.

## Risk score hook

On ingest, composite risk blends:

- Camera/dashcam confidence
- Latest `AiPredictions` row (`PredictionType = driver_risk`) when present
- OBD supporting heuristics

Fleet manager and super admin ERP lists show `driverRiskScore` and `compositeRiskScore`.

## Roles

- **Super admin:** `GET /api/fatigue-detection/platform/events` (all tenants, optional filter).
- **Fleet manager / dispatch:** `GET /api/fatigue-detection/events` (tenant isolated).
- **Driver:** Mobile ack + optional on-device camera prototype only.

## Local camera prototypes

- **Flutter:** `Frontend/sheikh-driver` — fatigue monitor screen (ML Kit face landmarks + EAR heuristic).
- **Python/OpenCV:** `tools/fatigue-camera-prototype/detect_fatigue.py` — desk testing; POST ingest manually.

## Device hooks

- `buzzer` — in-cab alert
- `engineStop` / relay — final auto-stop stage (subject to device capabilities and approval rules)

**Ali bhai, aapka ye kaam bacha hai** — the full production-grade specialized camera AI model still needs a dedicated team; this repo ships the integration framework and working prototypes only.
