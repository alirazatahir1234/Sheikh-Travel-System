#!/usr/bin/env bash
# Run SheikhGo Fleet on a physical / wireless Android device (e.g. Samsung A05).
# Auto-detects Mac LAN IP and points the app at http://{LAN}:5082/api.
#
# Usage:
#   ./scripts/run_android_device.sh
#   ./scripts/run_android_device.sh 192.168.100.59:41619
#   DEV_LAN_HOST=192.168.100.61 ./scripts/run_android_device.sh
set -euo pipefail
cd "$(dirname "$0")/.."

export ANDROID_HOME="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
export PATH="$PATH:$ANDROID_HOME/platform-tools"

DEVICE_ID="${1:-}"
LAN_HOST="${DEV_LAN_HOST:-}"

if [[ -z "$LAN_HOST" ]]; then
  LAN_HOST="$(ipconfig getifaddr en0 2>/dev/null || true)"
fi
if [[ -z "$LAN_HOST" ]]; then
  LAN_HOST="$(ipconfig getifaddr en1 2>/dev/null || true)"
fi
if [[ -z "$LAN_HOST" ]]; then
  echo "Could not detect Mac LAN IP. Set DEV_LAN_HOST=192.168.x.x and retry."
  exit 1
fi

API_BASE_URL="${API_BASE_URL:-http://${LAN_HOST}:5082/api}"

if [[ -z "$DEVICE_ID" ]]; then
  # Prefer first non-emulator Android device (wireless/USB).
  DEVICE_ID="$(
    adb devices | awk '/^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+:/{print $1; exit}
                      /^[A-Za-z0-9]+[[:space:]]+device$/{ if ($1 !~ /^emulator-/) { print $1; exit } }'
  )"
fi

if [[ -z "$DEVICE_ID" ]]; then
  echo "No physical Android device found."
  echo "1) Enable Wireless debugging / USB debugging"
  echo "2) adb pair <ip>:<pairing_port>  then  adb connect <ip>:<port>"
  echo "3) adb devices"
  exit 1
fi

echo "Device:  $DEVICE_ID"
echo "API:     $API_BASE_URL"
echo "Ensure ASP.NET API is listening on 0.0.0.0:5082 (launchSettings http/https)."

flutter run -d "$DEVICE_ID" \
  --dart-define=ENV=dev \
  --dart-define=API_BASE_URL="$API_BASE_URL" \
  --dart-define=DEV_LAN_HOST="$LAN_HOST"
