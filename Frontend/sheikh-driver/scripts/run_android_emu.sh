#!/usr/bin/env bash
# Reliable Android emulator run for SheikhGo Fleet.
# Avoids `flutter run -d all` (pulls in macOS) and flaky live device attach.
set -euo pipefail
cd "$(dirname "$0")/.."

export ANDROID_HOME="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
export PATH="$PATH:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator"

API_BASE_URL="${API_BASE_URL:-http://10.0.2.2:5082/api}"

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
flutter build apk --debug \
  --dart-define=API_BASE_URL="$API_BASE_URL" \
  --dart-define=ENV=dev

APK=build/app/outputs/flutter-apk/app-debug.apk
echo "Installing $APK..."
adb install -r -d "$APK"
adb shell monkey -p com.sheikhgo.fleet -c android.intent.category.LAUNCHER 1
echo "Android app launched. API=$API_BASE_URL"
