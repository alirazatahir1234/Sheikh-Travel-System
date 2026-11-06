# SheikhGo SMS Viewer (local ADB)

Small local tool to **list SMS on a USB-connected Android phone** using `adb`.

---

## ⚠️ Critical limitation — remote tracker SIM

**ADB cannot read SMS from a SIM that is physically inside a remote vehicle tracker** (for example a **Jimi VG03** sitting in a vehicle far away).

This tool only reads the **SMS database on the phone plugged into your Mac/PC** (`content://sms/` on that device). If Ali’s tracker SIM is in the VG03, this script will **not** see those messages.

| Goal | Works with this tool? |
|------|------------------------|
| Read SMS inbox on a phone connected via USB | ✅ Yes |
| Read SMS while that SIM is inside a remote Jimi VG03 / similar tracker | ❌ No |

**Practical options for tracker-SIM SMS:**

1. **Move the SIM** into an Android phone, enable USB debugging, run this tool (or use the phone’s Messages app).
2. **Tracker → phone replies** — configure the tracker so command replies / alerts are sent **to Ali’s phone** number; then read them here (or in Messages).
3. **Future SMS gateway** — operator/API or modem gateway that receives SMS for that number (not built here).

---

## Requirements

- Python 3.9+ (stdlib only — no pip packages)
- [Android platform-tools](https://developer.android.com/tools/releases/platform-tools) (`adb` on `PATH`, or pass `--adb`)
- An Android phone with **USB debugging** enabled

```bash
# macOS example
brew install android-platform-tools
```

---

## Setup (phone)

1. On the phone: **Settings → About phone** → tap **Build number** ~7 times to enable Developer options.
2. **Settings → Developer options** → enable **USB debugging**.
3. Connect the phone with a data-capable USB cable.
4. Unlock the phone and tap **Allow** / trust this computer when prompted.
5. Verify:

```bash
adb devices
# Expect: <serial>    device
```

If you see `unauthorized`, accept the trust dialog and retry.

---

## Usage

From the repo root (or this folder):

```bash
cd Tools/sms-viewer

# List connected devices
python3 sms_viewer.py --list-devices

# Pretty table (newest first)
python3 sms_viewer.py

# Specific device when several are plugged in
python3 sms_viewer.py --serial <SERIAL>

# Inbox only, last 30
python3 sms_viewer.py --box inbox --limit 30

# JSON (good for piping / scripts)
python3 sms_viewer.py --json

# Simple local HTML page (127.0.0.1 only)
python3 sms_viewer.py --serve --port 8765
```

Optional flags:

| Flag | Meaning |
|------|---------|
| `--serial` / `-s` | ADB serial when multiple devices are connected |
| `--adb` | Path to `adb` if not on `PATH` |
| `--box all\|inbox\|sent` | Filter by box |
| `--limit` / `-n` | Cap message count |
| `--json` | Machine-readable output |
| `--serve` | Local HTML table via `http.server` |
| `--quiet` | Hide the limitation banner on stderr |

---

## What it does

1. Resolves one online `adb` device (or uses `--serial`).
2. Runs roughly:  
   `adb shell content query --uri content://sms/ --projection address:date:body:type:...`
3. Parses rows into **address**, **date**, **body**, **type** (`inbox` / `sent` / …).
4. Prints a table, JSON, or a one-shot HTML page.

No secrets are stored. No keyloggers or background agents. SMS stay on the phone; the tool only queries over USB when you run it.

---

## OEM / permission notes

Some manufacturers restrict `content query` for SMS over ADB. If you get empty results but the Messages app shows SMS:

- Confirm you are on the **same user profile** as the Messages app (not a locked work profile).
- Try another cable/port; ensure the device state is `device` not `unauthorized`.
- As a fallback, export or forward messages from the Messages app, or use option (1)/(2) above for tracker SIMs.

---

## Files

| Path | Role |
|------|------|
| `sms_viewer.py` | CLI + optional local HTML server |
| `README.md` | This guide |
