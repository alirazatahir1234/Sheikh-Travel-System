# Gmeet Offline / No-GPRS SOS SMS

When a **Gmeet** tracker stops sending GPS data (stale `VehicleCurrentLocation` — typically lost data package / no GPRS), SheikhGo SMS the device’s **registered SOS number**. This is separate from ACC ignition SMS (Admin/Dispatcher `Users.Phone`).

Traccar’s SMS notificator is **not** used (`textEnabled=false`). Delivery uses the same site-side `Notifications:Sms` path as ignition alerts.

## Findings

### 1) How Gmeet is identified

Before this work, **no “Gmeet” string** existed in SheikhGo (catalog, models, or docs). Known brands were Jimi (VG03), Sinotrack (ST-901), Teltonika, Concox, etc.

**Mapping added:**

| Signal | Value |
|--------|--------|
| Brand | `Gmeet` (`TrackerBrands`) |
| Catalog key | `gmeet_generic` (`TrackerModels`) |
| Default protocol | `gt06` / port `5023` (common white-label; retarget in catalog if Ali’s units differ) |
| Match rules | Catalog/key prefix `gmeet_`, or brand/vendor/name/model contains `gmeet`, plus optional `GpsSettings:GmeetOfflineSms:ExtraNameContains` |

Assign the Gmeet brand/model on the device in ERP, or ensure Name/Vendor contains “Gmeet”.

### 2) Offline detection → SMS

| Layer | Behavior |
|-------|----------|
| Detector | `GpsOfflineDetectionHostedService` (background poller) |
| Condition | `VehicleCurrentLocation.LastUpdate` older than stale minutes |
| Dedup | Open `GpsAlertEvents` with `EventType=vehicle_offline` and `IsAcknowledged=0` (one SMS per offline transition; clears when GPS resumes) |
| Ops alerts | Unchanged — Admin/Dispatcher InApp/Browser/Push via `NotificationDecisionEngine` |
| Gmeet SOS SMS | Extra path: SMS to device SOS only when matcher + resolved phone |

Default scan: **60s** when Gmeet SMS enabled (was 5 min). Stale default: **`GpsSettings:OfflineStaleMinutes`** (30).

### 3) SMS mechanism

Same as ACC ignition: `SmsNotificationSender` → `Notifications:Sms` (`Console` log-only or `Http` live gateway).

## SOS number resolution

```text
GpsDevices.SosNumber  →  phone-like GpsDevices.Contact  →  skip (log warning)
```

- **`Phone`** = tracker SIM — **never** used as SOS recipient.
- Numbers are **not invented**. Ali must set `SosNumber` (or a digit Contact) in tracker register/edit.
- Device-side SOS SMS commands (SIM → phone) remain separate and are not read from Traccar attributes in this MVP.

## Config Ali must set

### 1. Live SMS gateway

Same keys as [ACC_IGNITION_SMS.md](./ACC_IGNITION_SMS.md):

| Key | Purpose |
|-----|---------|
| `Notifications:Sms:Enabled` | `true` for live send |
| `Notifications:Sms:Provider` | `Http` (or `Console` for log-only) |
| `Notifications:Sms:Http:BaseUrl` / `ApiKey` / field maps | Gateway — **do not commit secrets** |

Until Http is configured, SMS is **logged only** (Console).

### 2. Device SOS number + Gmeet identity

1. Register/edit tracker → Brand **Gmeet** (or name/vendor containing Gmeet).
2. Set **SOS number** field (`GpsDevices.SosNumber`).
3. Link device to a vehicle with live Traccar ingest.

### 3. Optional tuning

```json
"GpsSettings": {
  "OfflineStaleMinutes": 30,
  "GmeetOfflineSms": {
    "Enabled": true,
    "StaleMinutes": 0,
    "ScanIntervalSeconds": 60,
    "ExtraNameContains": []
  }
}
```

`StaleMinutes: 0` → use `OfflineStaleMinutes`.

## Debounce

| Layer | Window |
|-------|--------|
| Open `vehicle_offline` alert | Until GPS online (ack on ingest) |
| Seeded `AiCooldownRules` `gmeet_device_offline` | 30 minutes (secondary) |

## Verification

1. Restart API so `GmeetOfflineSosSmsMigration` runs (`SosNumber` column, Gmeet catalog, `gmeet_offline_sos` template).
2. Set a Gmeet device’s `SosNumber`; ensure vehicle has a recent then stale location (or wait past stale minutes with GPRS off).
3. With `Notifications:Sms:Enabled=false`: check logs for `SMS [Console] … Gmeet tracker offline` and masked phone.
4. With Http gateway: SMS arrives at SOS number; ops InApp offline alert still fires for Admin/Dispatcher.

## Limitations

- Gmeet protocol may not be GT06 — confirm with Traccar device list / raw packets; update `TrackerModels` if needed.
- SOS number set only **on the hardware** (SMS to SIM) is **not** auto-synced into SheikhGo; ERP `SosNumber` must be filled.
- Latency ≈ scan interval + stale minutes (not sub-second).
- Non-Gmeet devices do not get this SOS SMS (ops offline InApp still applies to all).
- Http SMS gateway still required for live delivery (same blocker as ignition SMS).
