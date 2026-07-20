# SheikhGo Driver (Flutter)

Driver login, assigned trips, start/complete, attendance, fuel, live GPS, SOS, and timeline for the SheikhGo fleet platform.

**Package:** `com.sheikhgo.driver`  
**Folder:** `Frontend/sheikh-driver`

## Prerequisites

- Flutter SDK 3.2+
- Driver user linked to `Drivers.UserId` in the database
- API running with tenant slug `default` (or set `TENANT_SLUG`)

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

## Run on iPhone Simulator

1. Open Simulator (or boot one):

```bash
open -a Simulator
# optional — pick a device
xcrun simctl boot "iPhone 17 Pro"
```

2. Quit any existing Flutter run, then from `Frontend/sheikh-driver`:

```bash
flutter devices
flutter run -d ios --dart-define=API_BASE_URL=http://127.0.0.1:5082/api
```

If several iOS devices appear, pick the simulator id from the list, or:

```bash
flutter run -d "iPhone 17 Pro" --dart-define=API_BASE_URL=http://127.0.0.1:5082/api
```

Requires Xcode installed and `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer` once.

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

## Store release (Phase N)

See `store/` for privacy, terms, listing copy, screenshots guide, release notes, and the full checklist.

```bash
# Android App Bundle (requires android/key.properties — see key.properties.example)
flutter build appbundle --release \
  --dart-define=ENV=prod \
  --dart-define=API_BASE_URL=https://api.example.com/api \
  --dart-define=PRIVACY_URL=https://example.com/driver/privacy \
  --dart-define=TERMS_URL=https://example.com/driver/terms \
  --dart-define=CERT_PIN_1=YOUR_SHA256_HEX

# iOS IPA
flutter build ipa --release \
  --dart-define=ENV=prod \
  --dart-define=API_BASE_URL=https://api.example.com/api \
  --dart-define=PRIVACY_URL=https://example.com/driver/privacy \
  --dart-define=TERMS_URL=https://example.com/driver/terms \
  --dart-define=CERT_PIN_1=YOUR_SHA256_HEX
```

In-app: **Settings → Privacy Policy / Terms of Service**. Host the markdown (or HTML) publicly and pass those URLs via `PRIVACY_URL` / `TERMS_URL`.
