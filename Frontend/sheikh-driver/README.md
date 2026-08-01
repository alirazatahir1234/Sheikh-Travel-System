# SheikhGo Fleet (Flutter)

Mobile fleet operations for drivers and staff: trips, live GPS, attendance, fuel, and role-based fleet navigation for the SheikhGo platform.

**Package:** `com.sheikhgo.fleet`  
**Folder:** `Frontend/sheikh-driver`

## Prerequisites

- Flutter SDK 3.2+
- Driver user linked to `Drivers.UserId` in the database
- API running with tenant slug `default` (or set `TENANT_SLUG`)

## Splash / native launch

After changing `assets/branding/sheikhgo_logo.png` or `flutter_native_splash` in `pubspec.yaml`:

```bash
dart run flutter_native_splash:create
```

The in-app splash route is `/splash` (2s minimum, then login or dashboard).

## Install Flutter (one-time, macOS)

If you see `zsh: command not found: flutter`, install the SDK first:

```bash
brew install --cask flutter
```

Add to `~/.zshrc` (Homebrew Apple Silicon):

```bash
export PATH="$PATH:/opt/homebrew/Caskroom/flutter/latest/flutter/bin"
```

Then open a **new terminal** and run `flutter doctor`.

### macOS: `"dartvm" Not Opened` (Gatekeeper)

If `flutter --version` shows `zsh: killed` and macOS blocks **dartvm**:

1. Click **Done** on the dialog (do not Move to Trash).
2. Open **System Settings → Privacy & Security**.
3. Scroll to **Security** and click **Allow Anyway** (or **Open Anyway**) for `dartvm` / Flutter.
4. Run again: `flutter --version`

Alternatively, after install:

```bash
xattr -dr com.apple.quarantine /opt/homebrew/Caskroom/flutter
```

## API base URL (`AppConfig.resolvedBaseUrl`)

Supports **Android emulator + physical Samsung (or any phone)** without editing source.

Resolution order:

1. Explicit `--dart-define=API_BASE_URL=...` (full URL always wins)
2. When `--dart-define=ENV=prod` or `ENV=uat`: Production HTTPS (same host as ERP)
3. `--dart-define=DEV_LAN_HOST=192.168.x.x` → `http://{host}:5082/api` (physical device)
4. Otherwise platform local defaults

| Target | Default |
|--------|---------|
| `ENV=prod` / `ENV=uat` | `https://sheikh-travel-system-production.up.railway.app/api` |
| Physical phone + `DEV_LAN_HOST` / `API_BASE_URL` | `http://{MacLAN}:5082/api` |
| Android emulator (`ENV=dev`, no LAN define) | `http://10.0.2.2:5082/api` |
| iOS Simulator / desktop (`ENV=dev`) | `http://localhost:5082/api` |

SignalR uses `AppConfig.hubBaseUrl` (`{apiOrigin}/hubs/tracking`, or `--dart-define=HUB_URL=...`).

**Local physical device against Mac API:** pass your Mac LAN IP — `localhost` / `10.0.2.2` will not reach the host. Ensure the API listens on `0.0.0.0:5082` (already set in `launchSettings.json`).

```bash
# Find Mac LAN IP
ipconfig getifaddr en0
```

**Production APK (any network, no Mac):**

```bash
./scripts/run_prod_device.sh
# or: ./scripts/build_prod.sh apk
```

Install `build/app/outputs/flutter-apk/app-release.apk` on any Android phone with internet.
## Run targets

From `Frontend/sheikh-driver` (or use the absolute path below).

### Android emulator

No dart-define required (uses `10.0.2.2`):

```bash
flutter devices
flutter run -d <android-emulator-id>
# or
./scripts/run_android_emu.sh
```

### Physical / wireless Android (Samsung A05)

Phone + Mac on the **same Wi‑Fi**. API must be up on `0.0.0.0:5082`.

```bash
# One-shot helper (auto-detects Mac LAN IP + first wireless/USB phone)
./scripts/run_android_device.sh

# Or explicit device + defines
LAN=$(ipconfig getifaddr en0)
flutter run -d 192.168.100.59:41619 \
  --dart-define=ENV=dev \
  --dart-define=API_BASE_URL=http://$LAN:5082/api
```

Equivalent short form:

```bash
flutter run -d <phone_id> --dart-define=DEV_LAN_HOST=$(ipconfig getifaddr en0)
```

### iOS Simulator

```bash
open -a Simulator
flutter devices
# Prefer the simulator UUID or name — `-d ios` is not a device id
flutter run -d "iPhone 17 Pro Max" \
  --dart-define=API_BASE_URL=http://127.0.0.1:5082/api
```

Requires Xcode and `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer` once.

### Physical / wireless iPhone

1. Xcode → **Window → Devices and Simulators** → enable **Connect via network**.
2. Same Wi‑Fi as the Mac. Trust the computer on the phone.

```bash
flutter devices
flutter run -d 00008120-001E6C1E0EA3601E \
  --dart-define=API_BASE_URL=http://YOUR_MAC_LAN_IP:5082/api
```

Example:

```bash
flutter run -d 00008120-001E6C1E0EA3601E \
  --dart-define=API_BASE_URL=http://10.171.139.91:5082/api
```

## App icon / branding

Launcher + login logo use the ERP brand asset (`public/brand/sheikhgo-logo.png`), copied to `assets/icon/app_icon.png`. Regenerate icons with:

```bash
dart run flutter_launcher_icons
```

```bash
cd Frontend/sheikh-driver
./setup.sh
flutter run --dart-define=API_BASE_URL=http://127.0.0.1:5082/api
```

If you are **already inside** `sheikh-driver`, do **not** run `cd Frontend/sheikh-driver` again — that path only works from the repo root.

**Absolute path:**

```bash
cd /Users/alirazatahir/Projects/Sheikh-Travel-System/Frontend/sheikh-driver
./setup.sh
```

The first `setup.sh` run runs `flutter create` to add `android/` and `ios/` folders (required before `flutter run`).

## Run without Xcode (recommended on Mac until Xcode is installed)

`flutter run -d macos` needs **full Xcode** (`xcodebuild`). If you see `unable to find utility "xcodebuild"`, use the **web server** target instead (no Chrome required):

```bash
cd Frontend/sheikh-driver
flutter config --enable-web
flutter run -d web-server --web-port=7357 \
  --dart-define=API_BASE_URL=http://127.0.0.1:5082/api
```

Open **http://localhost:7357** in Safari or any browser.

`-d chrome` only works if Google Chrome is installed.

## Install Xcode (for macOS / iOS app builds)

1. Install **Xcode** from the Mac App Store (large download).
2. Run: `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer`
3. Open Xcode once and accept the license.
4. Then: `flutter run -d macos --dart-define=API_BASE_URL=http://127.0.0.1:5082/api`

## Clear Xcode “Stale file” warnings

These are usually DerivedData / Pods cache noise after Flutter rebuilds, not app bugs:

```bash
cd Frontend/sheikh-driver
flutter clean
rm -rf ios/Pods ios/.symlinks ios/Flutter/Flutter.framework ios/Flutter/Flutter.podspec
rm -rf ~/Library/Developer/Xcode/DerivedData/*
flutter pub get
cd ios && pod install && cd ..
```

## Maps (Phase C)

Trip detail → **Open Trip Map** opens in-app Google Maps with live location, route line, traffic toggle, ETA (`GET /gps/eta`), and deep links to Google Maps / Apple Maps / Waze.

Set your Maps API key:

```bash
# Android — android/local.properties
GOOGLE_MAPS_API_KEY=your_key_here

# iOS — set GMSApiKey in Info.plist or Xcode build setting GOOGLE_MAPS_API_KEY
# Run with:
flutter run --dart-define=API_BASE_URL=http://127.0.0.1:5082/api --dart-define=GOOGLE_MAPS_KEY=your_key_here
```

- `POST /api/driver-app/auth/login`
- `GET /api/driver-app/profile`
- `GET /api/driver-app/dashboard`
- `GET /api/driver-app/trips`
- `POST /api/driver-app/trips/{id}/start|complete|reject`
- `POST /api/driver-app/trips/location`
- `POST /api/driver-app/location/batch`
- `POST /api/driver-app/sos`
- `GET /api/driver-app/timeline`
- `GET /api/driver-app/earnings`
- `POST /api/driver-app/attendance/check-in|check-out`
- `GET /api/driver-app/attendance/history`
- `POST /api/driver-app/fuel-receipts`
- `GET /api/driver-app/notifications`

## Store release (Phase N / Sprint 6)

See `store/` for privacy, terms, listing copy, screenshots guide, release notes, and the full checklist.

**Bundle ID:** `com.sheikhgo.fleet`

### Sprint 6 features

- **Biometric lock** — Settings → Security (Face ID / fingerprint); locks after ~15s in background
- **Offline queue** — auto-sync on reconnect + manual retry for failed/conflict items
- **i18n** — English + Arabic (Settings → Language); RTL for Arabic via Flutter Material
- **Prod builds** — `scripts/build_prod.sh`

```bash
# Release APK for any physical device (defaults to Production Railway API)
./scripts/run_prod_device.sh

# Android App Bundle (requires android/key.properties — see key.properties.example)
# API_BASE_URL defaults to https://sheikh-travel-system-production.up.railway.app/api
export PRIVACY_URL=https://example.com/fleet/privacy
export TERMS_URL=https://example.com/fleet/terms
export CERT_PIN_1=YOUR_SHA256_HEX   # optional; without pins HTTPS still works, pinning is off
./scripts/build_prod.sh android

# Or direct flutter:
flutter build appbundle --release \
  --dart-define=ENV=prod \
  --dart-define=API_BASE_URL=https://sheikh-travel-system-production.up.railway.app/api \
  --dart-define=PRIVACY_URL=https://example.com/fleet/privacy \
  --dart-define=TERMS_URL=https://example.com/fleet/terms \
  --dart-define=CERT_PIN_1=YOUR_SHA256_HEX

# iOS IPA
./scripts/build_prod.sh ios
```

In-app: **Settings → Privacy Policy / Terms of Service**. Host the markdown (or HTML) publicly and pass those URLs via `PRIVACY_URL` / `TERMS_URL`.
