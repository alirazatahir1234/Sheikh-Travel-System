# Tracker store-and-forward (VG03 / ST-901)

Honest MVP: **flash buffering lives on the tracker firmware**, not in Traccar. Traccar receives points when devices send them. It cannot write device flash, cannot implement “oldest deleted first” on the unit, and cannot ACK-before-delete except by speaking the device protocol correctly.

## Desired behavior (device firmware)

1. Online → each GPS point uploaded immediately  
2. Offline / no GPRS → points saved on tracker flash  
3. Flash full → **FIFO** (oldest dropped, fresh kept)  
4. Back online → backlog uploaded (often in small batches)  
5. Local delete only after server protocol ACK (device-dependent)

## What Traccar can do

| Lever | Effect |
|-------|--------|
| Correct protocol/port | ST-901 must use **h02 / TCP 5013** (not gt06/5023) or buffered H02 uploads never decode |
| `h02.ack=true` | Server ACKs H02 binary frames so the unit can drop confirmed backlog (global XML and/or device attr) |
| `server.buffering.threshold` (ms) | Server-side **reorder** of late/out-of-order backlog points — not device flash |
| Device attributes / notes | Document expected firmware policy for ops |
| GT06/Jimi protocol | Login/position packets already use protocol ACKs in Traccar’s gt06 decoder |

## JM-VG03 (device id 19, IMEI `862292058090598`)

| Topic | Finding |
|-------|---------|
| Offline cache | Jimi/Concox FAQ: blind-area FIFO cache, auto-upload on reconnect; capacity varies by model |
| Hardware | Specs list **32+32 Mb** data storage |
| SMS toggle | **No** public `BFFREQ` / history-upload SMS in JM-VG03 user manual; buffer is **automatic** |
| Useful SMS | `TIMER,T1,T2#` (live interval), `SERVER,…#`, `APN,…#`, `STATUS#`, `GPRSSET#` — not buffer enable |
| Traccar attrs applied | `storeAndForward=true`, `storeAndForwardOwner=device_firmware`, `expectedProtocol=gt06`, `offlineBufferNote=…` |
| SMS from Traccar | `textEnabled=false` — no gateway |

## ST-901 / SOGOTRACK AUC-513 (device id 22, IMEI `865023071369959`)

| Topic | Finding |
|-------|---------|
| Protocol | **H02 / 5013** (Traccar device list). Wrong gt06:5023 breaks buffer upload path |
| Offline buffer | Vendor/community: onboard store-and-forward (often cited ~2000 points); FIFO on firmware |
| Batch upload | SinoTrack GPRS `D1` supports `batch` 1–16 records; needs correct center password / online session |
| SMS | Typical `80x` + 4-digit PIN (factory often `0000`). Unit may be **password-locked** — do not claim SMS applied |
| Traccar attrs applied | `h02.ack=true`, `expectedProtocol=h02`, `expectedPort=5013`, store-and-forward documentation attrs |
| SMS from Traccar | Impossible while `textEnabled=false` |

## Server XML (SheikhGo VM Traccar 6.14.5)

Applied globally in `/opt/traccar/conf/traccar.xml` (2026-10-26):

```xml
<entry key='h02.ack'>true</entry>
<entry key='server.buffering.threshold'>5000</entry>
```

Backup created beside the file as `traccar.xml.bak.sheikhgo-saf-*`. Restart required (done).

## Explicit non-claims

- Traccar does **not** implement tracker flash buffering.  
- Setting device attributes alone does **not** create offline storage.  
- Without unlocking ST-901 SMS PIN (or GPRS command while online), interval/server SMS cannot be confirmed.
