# Sheikh Driver App (Flutter)

Driver login, assigned trips, start/complete, and background GPS to `POST /api/driver-app/trips/location`.

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

## Run

**From the repo root** (`Sheikh-Travel-System`):

```bash
cd Frontend/sheikh-driver-app
./setup.sh
flutter run --dart-define=API_BASE_URL=http://127.0.0.1:5082/api
```

If you are **already inside** `sheikh-driver-app`, do **not** run `cd Frontend/sheikh-driver-app` again — that path only works from the repo root.

**Absolute path:**

```bash
cd /Users/alirazatahir/Projects/Sheikh-Travel-System/Frontend/sheikh-driver-app
./setup.sh
```

The first `setup.sh` run runs `flutter create` to add `android/` and `ios/` folders (required before `flutter run`).

## Run without Xcode (recommended on Mac until Xcode is installed)

`flutter run -d macos` needs **full Xcode** (`xcodebuild`). If you see `unable to find utility "xcodebuild"`, use the **web server** target instead (no Chrome required):

```bash
cd Frontend/sheikh-driver-app
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

## API

- `POST /api/driver-app/auth/login`
- `GET /api/driver-app/trips`
- `POST /api/driver-app/trips/{id}/start|complete`
- `POST /api/driver-app/trips/location`
