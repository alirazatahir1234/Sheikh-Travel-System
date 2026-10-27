# ACC / Ignition SMS Alerts (SheikhGo)

SheikhGo sends **ignition ON** and **ignition OFF** SMS through the same notification pipeline as SOS / speed alerts. Traccar’s own SMS notificator is **not** used (Traccar has no SMS gateway; `textEnabled=false`).

## Pipeline

```text
Vehicle ACC wire → GPS tracker (e.g. Jimi VG03)
        → Traccar attributes.ignition
        → TraccarSyncOrchestrator → IngestPositionAsync
        → EvaluateIgnitionTransitionAsync
              ├─ GpsAlertEvents (ignition_on / ignition_off)
              └─ NotificationDecisionEngine (priority ≥ 3, Sms allowed)
                    → NotificationDispatchHostedService
                    → SmsNotificationSender (Console | Http)
```

Ignition transitions are detected from **position-state changes** (`VehicleCurrentLocation.Ignition` → new fix), not from Traccar `ignitionOn`/`ignitionOff` event types.

## Relationship to Traccar web/mail

Traccar notifications #1 `ignitionOff` / #2 `ignitionOn` (channels **web, mail**) remain unchanged. SheikhGo SMS is an **additional** path for ops users — it does not replace or disable Traccar UI/mail alerts.

ST-901 Acc Off SMS (device SIM → phone) is separate and device-originated.

## Recipients

- Tenant **Admin** + **Dispatcher** (and platform role codes via `NotificationRecipientResolver`).
- SMS dials **`Users.Phone`** for each recipient (looked up by `SmsNotificationSender` when `Phone` is not passed).
- Per-user opt-out: `AlertSettings` row with `SmsEnabled = 0` for `ignition_on` / `ignition_off` (default = allow when no row).

## Debounce

| Layer | Window |
|-------|--------|
| `GpsAlertEvents` same-type dedup | 2 minutes |
| `AiCooldownRules` / Redis decision cooldown | 5 minutes (seeded) |

## Config Ali must set

### 1. Live SMS gateway (blocker for real SMS)

`appsettings` / env / user secrets under `Notifications:Sms`:

| Key | Purpose |
|-----|---------|
| `Enabled` | `true` to leave Console log-only mode |
| `Provider` | `Console` (dev) or `Http` (live gateway) |
| `DefaultFrom` / `Http:From` | Sender ID if gateway requires it |
| `Http:BaseUrl` | Gateway send URL |
| `Http:ApiKey` | Secret — **do not commit** |
| `Http:AuthScheme` | `ApiKey` \| `Bearer` \| `Basic` |
| `Http:ToField` / `BodyField` / `FromField` | Map to provider field names |
| `Http:ContentType` | `application/json` or form-urlencoded |

Until `Enabled=true` and `Provider=Http` with a valid `BaseUrl` + credentials, SMS is **logged only** (Console).

Tenant ERP settings keys `SmsEnabled` / `SmsGatewayApiKey` are UI placeholders; live send uses **`Notifications:Sms`** above.

### 2. Recipient phones

Ensure Admin/Dispatcher users have **`Users.Phone`** populated (e.g. `03016169209`). Empty phone → SMS delivery fails for that user; InApp/Browser/Push still work.

### 3. Physical ACC wire (VG03)

IMEI `862292058090598` — ACC wire must be installed so Traccar reports `attributes.ignition`. Until then, SheikhGo will not see ON/OFF transitions and will not SMS. Device `InstallationNotes` document that SheikhGo is ready once ACC reports.

## Automatic after wiring?

**Yes**, once:

1. ACC sensor is wired and the tracker reports `ignition` true/false on positions.
2. Vehicle is linked in SheikhGo (IMEI → `GpsDevices` → `Vehicles`, with `TenantId`).
3. API is running with Traccar sync enabled.
4. For **live** SMS: `Notifications:Sms` Http gateway configured + recipient `Users.Phone` set.

No extra “enable ignition SMS” feature flag is required; templates and cooldown rules are seeded by `IgnitionSmsAlertsMigration`.

## Verification

1. Restart API so `IgnitionSmsAlertsMigration` runs.
2. Confirm templates: `ignition_on_alert` / `ignition_off_alert` (InApp + Sms) in `NotificationTemplates`.
3. Confirm `AiCooldownRules` for `ignition_on` / `ignition_off`.
4. With `Notifications:Sms:Enabled=false` (Console): toggle ignition in a test ingest or wait for a real ACC transition → check logs for `SMS [Console] … Ignition ON/OFF` and a `GpsAlertEvents` row.
5. With Http gateway + phones: same transition should deliver SMS; check `AlertNotificationLogs` / `Notifications` delivery status.

## Limitations

- No Twilio-specific client — use generic **Http** mapping for Twilio or a local Pakistani SMS API.
- OTP SMS (`ConsoleSmsOtpService`) is separate and still log-only until wired to the same gateway.
- `GpsTelemetryNormalizer` may force ignition OFF when `motion=false` at low speed (ACC stuck-ON mitigation) — can create extra OFF/ON edges; 2-minute dedup + 5-minute cooldown reduce SMS spam.
- Traccar SMS notificator remains unavailable without a Traccar SMS gateway (by design; SheikhGo owns SMS).
