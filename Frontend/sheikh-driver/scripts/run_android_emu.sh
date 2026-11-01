#!/usr/bin/env bash
# Reliable Android emulator run for SheikhGo Tracking (local API via 10.0.2.2).
# Avoids `flutter run -d all` (pulls in macOS) and flaky live device attach.
set -euo pipefail
cd "$(dirname "$0")/.."

export ANDROID_HOME="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
export PATH="$PATH:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator"

# Emulator → host loopback. Override with a stable HTTPS host if needed:
#   API_BASE_URL=https://joke-rebates-titans-duties.trycloudflare.com/api ./scripts/run_android_emu.sh
# Do NOT use release AAB on emulator — ENV=prod blocks emulators.
API_BASE_URL="${API_BASE_URL:-http://10.0.2.2:5082/api}"

# Routes API client fallback (map tiles already use android/maps.properties).
GOOGLE_MAPS_KEY="${GOOGLE_MAPS_KEY:-}"
if [[ -z "$GOOGLE_MAPS_KEY" && -f android/maps.properties ]]; then
  GOOGLE_MAPS_KEY="$(grep -E '^GOOGLE_MAPS_API_KEY=' android/maps.properties | cut -d= -f2- | tr -d '\r')"
fi

if ! adb devices | grep -qE 'emulator-[0-9]+[[:space:]]+device'; then
  echo "Starting Android emulator Medium_Phone_API_36.1..."
  nohup "$ANDROID_HOME/emulator/emulator" -avd Medium_Phone_API_36.1 \
    -no-snapshot-load -no-audio >/tmp/sheikh-avd.log 2>&1 &
  adb wait-for-device
  until [[ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" == "1" ]]; do
    sleep 2
  done
fi

# Belt-and-suspenders: also map emulator localhost → host :5082 (for 127.0.0.1 overrides).
adb reverse tcp:5082 tcp:5082 >/dev/null 2>&1 || true

echo "Checking host API on :5082..."
# Prefer /health (200). Login probe used to print curl: (22) 400 false alarms.
api_code="$(curl -sS -o /dev/null -w '%{http_code}' --connect-timeout 2 --max-time 5 \
  "http://127.0.0.1:5082/health" 2>/dev/null || echo "000")"
if [[ "$api_code" != "200" ]]; then
  echo "WARNING: nothing healthy on http://127.0.0.1:5082/health (got HTTP $api_code) — start the local API first."
  echo "  (launchSettings binds http://0.0.0.0:5082)"
fi

echo "Building debug APK..."
DEFINES=(
  --dart-define=API_BASE_URL="$API_BASE_URL"
  --dart-define=ENV=dev
)
if [[ -n "$GOOGLE_MAPS_KEY" ]]; then
  DEFINES+=(--dart-define=GOOGLE_MAPS_KEY="$GOOGLE_MAPS_KEY")
fi
flutter build apk --debug "${DEFINES[@]}"

APK=build/app/outputs/flutter-apk/app-debug.apk
echo "Installing $APK..."
adb install -r -d "$APK"
adb shell monkey -p com.sheikhgo.fleet -c android.intent.category.LAUNCHER 1
echo ""
echo "Android app launched."
echo "  API=$API_BASE_URL"
echo "  Seed login: admin@sheikhtravel.com / Pass@123"
echo "  (driver phone: 03021234567 / Pass@123)"
echo "  Do NOT use release AAB/APK on emulator — prod release blocks emulators;"
echo "  release defaults to https://joke-rebates-titans-duties.trycloudflare.com/api (physical devices)."
