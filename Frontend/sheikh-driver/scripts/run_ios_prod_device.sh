#!/usr/bin/env bash
# Install a **release** build of SheikhGo Tracking on a USB-connected iPhone,
# pointed at production HTTPS (same host as ERP). No TestFlight required.
#
# Usage:
#   ./scripts/run_ios_prod_device.sh              # auto-pick first iOS device
#   ./scripts/run_ios_prod_device.sh <device-id>  # flutter devices id / UDID
#
# Debug / local API (unchanged):
#   ./scripts/run_ios_sim.sh
#   flutter run --dart-define=ENV=dev --dart-define=DEV_LAN_HOST=<MacLAN>
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

DEFAULT_PROD_API='https://20-174-1-230.sslip.io/api'
API_BASE_URL="${API_BASE_URL:-$DEFAULT_PROD_API}"
ENV_NAME="${ENV:-prod}"

case "$API_BASE_URL" in
  *railway.app*|*trycloudflare.com*)
    echo "ERROR: Refusing retired/stale API host: $API_BASE_URL"
    echo "  Use: $DEFAULT_PROD_API"
    exit 1
    ;;
esac

DEVICE_ARG=()
if [[ "${1:-}" != "" ]]; then
  DEVICE_ARG=(-d "$1")
else
  # Prefer a physical iPhone if flutter can see one.
  # flutter devices lines look like: "iPhone (mobile) • UDID • ios • iOS …"
  # (mobile appears before ios — do not require ios.*mobile.)
  PHYS="$(flutter devices 2>/dev/null | grep -E '• ios([[:space:]]|$)' | grep -vi simulator | head -1 || true)"
  if [[ -n "$PHYS" ]]; then
    DID="$(echo "$PHYS" | awk -F'•' '{print $2}' | xargs)"
    if [[ -n "$DID" ]]; then
      DEVICE_ARG=(-d "$DID")
      echo "==> Using device: $DID"
    fi
  fi
fi

APP_VERSION_NAME="$(grep -E '^version:' pubspec.yaml | head -1 | awk '{print $2}' | cut -d+ -f1)"
APP_VERSION_CODE="$(grep -E '^version:' pubspec.yaml | head -1 | awk '{print $2}' | cut -d+ -f2)"
APP_VERSION_LABEL="${APP_VERSION_NAME}+${APP_VERSION_CODE}"

DEFINES=(
  "--dart-define=ENV=${ENV_NAME}"
  "--dart-define=API_BASE_URL=${API_BASE_URL}"
  "--dart-define=TENANT_SLUG=${TENANT_SLUG:-default}"
  "--dart-define=APP_VERSION=${APP_VERSION_LABEL}"
)

if [[ -n "${HUB_URL:-}" ]]; then
  DEFINES+=("--dart-define=HUB_URL=${HUB_URL}")
fi
if [[ -n "${GOOGLE_MAPS_KEY:-${GOOGLE_MAPS_API_KEY:-}}" ]]; then
  DEFINES+=("--dart-define=GOOGLE_MAPS_KEY=${GOOGLE_MAPS_KEY:-${GOOGLE_MAPS_API_KEY}}")
fi

echo "==> SheikhGo Tracking iOS RELEASE → ${API_BASE_URL}"
echo "    ENV=${ENV_NAME} version=${APP_VERSION_LABEL}"
echo "    Trust this Mac on the iPhone if prompted; signing uses team in ios/Runner.xcodeproj"
flutter pub get
# macOS bash 3.2 + set -u: empty DEVICE_ARG[@] is "unbound variable"
if ((${#DEVICE_ARG[@]} > 0)); then
  flutter run --release "${DEVICE_ARG[@]}" "${DEFINES[@]}"
else
  echo "WARN: No -d device id set; flutter will pick default (pass UDID if multiple devices)."
  flutter devices
  flutter run --release "${DEFINES[@]}"
fi
