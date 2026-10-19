#!/usr/bin/env bash
# Reliable Android emulator run for SheikhGo Tracking.
# Avoids `flutter run -d all` (pulls in macOS) and flaky live device attach.
set -euo pipefail
cd "$(dirname "$0")/.."

export ANDROID_HOME="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
export PATH="$PATH:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator"

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
echo "Android app launched. API=$API_BASE_URL"
