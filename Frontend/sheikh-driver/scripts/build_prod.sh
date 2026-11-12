#!/usr/bin/env bash
# Production / UAT release builds for SheikhGo Tracking.
# Usage:
#   ./scripts/build_prod.sh android
#   ./scripts/build_prod.sh ios
#   ./scripts/build_prod.sh apk
#   ./scripts/build_prod.sh android uat
# Optional overrides:
#   API_BASE_URL=https://... HUB_URL=https://.../hubs ./scripts/build_prod.sh apk
#
# Required for maps (Android native SDK key):
#   android/maps.properties  OR  GOOGLE_MAPS_API_KEY / GOOGLE_MAPS_KEY env
# Optional for FCM/Crashlytics:
#   android/app/google-services.json  (see android/app/GOOGLE_SERVICES.md)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

TARGET="${1:-android}"
ENV_NAME="${2:-prod}"

# Mobile prod default matches Fleet: cleartext Kestrel on Azure VM :5082
# (network_security_config allows 20.174.1.230). Do NOT default to:
# - retired Railway / stale *.trycloudflare.com (NXDOMAIN when tunnel dies)
# - https://20.174.1.230 (cert SAN is only 20-174-1-230.sslip.io)
# Prefer later: https://api.sheikhgo.com/api once DNS is live.
# ERP browsers keep HTTPS (sslip / api.sheikhgo.com) — Mixed Content.
DEFAULT_PROD_API='http://20.174.1.230:5082/api'
API_BASE_URL="${API_BASE_URL:-$DEFAULT_PROD_API}"
case "$API_BASE_URL" in
  *railway.app*|*trycloudflare.com*)
    echo "ERROR: Refusing retired/stale API host: $API_BASE_URL"
    echo "  Use: $DEFAULT_PROD_API (or https://api.sheikhgo.com/api when DNS is live)"
    exit 1
    ;;
esac

# Native Maps key → Gradle manifest placeholder (android/maps.properties).
# Dart Directions/ETA key → --dart-define=GOOGLE_MAPS_KEY.
GOOGLE_MAPS_KEY="${GOOGLE_MAPS_KEY:-${GOOGLE_MAPS_API_KEY:-}}"
if [[ -z "$GOOGLE_MAPS_KEY" && -f android/maps.properties ]]; then
  GOOGLE_MAPS_KEY="$(grep -E '^GOOGLE_MAPS_API_KEY=' android/maps.properties | cut -d= -f2- | tr -d '\r' || true)"
fi
if [[ -z "$GOOGLE_MAPS_KEY" && -f android/local.properties ]]; then
  GOOGLE_MAPS_KEY="$(grep -E '^GOOGLE_MAPS_API_KEY=' android/local.properties | cut -d= -f2- | tr -d '\r' || true)"
fi

# Keep in-app footer / User-Agent in sync with pubspec (x.y.z+build).
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
if [[ -n "${GOOGLE_MAPS_KEY}" ]]; then
  DEFINES+=("--dart-define=GOOGLE_MAPS_KEY=${GOOGLE_MAPS_KEY}")
  # Ensure Gradle manifest placeholder is set even without maps.properties copy
  # on CI agents that only export the env var.
  export GOOGLE_MAPS_API_KEY="${GOOGLE_MAPS_KEY}"
else
  echo "WARNING: No Google Maps key found."
  echo "  Blank maps in release — set GOOGLE_MAPS_API_KEY or create android/maps.properties"
  echo "  (see android/maps.properties.example)."
fi
if [[ -n "${PRIVACY_URL:-}" ]]; then
  DEFINES+=("--dart-define=PRIVACY_URL=${PRIVACY_URL}")
fi
if [[ -n "${TERMS_URL:-}" ]]; then
  DEFINES+=("--dart-define=TERMS_URL=${TERMS_URL}")
fi
if [[ -n "${CERT_PIN_1:-}" ]]; then
  DEFINES+=("--dart-define=CERT_PIN_1=${CERT_PIN_1}")
fi
if [[ -n "${CERT_PIN_2:-}" ]]; then
  DEFINES+=("--dart-define=CERT_PIN_2=${CERT_PIN_2}")
fi

if [[ ! -f android/app/google-services.json ]]; then
  echo "WARNING: android/app/google-services.json missing — Firebase/FCM/Crashlytics disabled."
  echo "  See android/app/GOOGLE_SERVICES.md (app still launches; push will not work)."
fi

echo "==> SheikhGo Tracking ${ENV_NAME} build (${TARGET})"
echo "    version=${APP_VERSION_LABEL}"
echo "    API_BASE_URL=${API_BASE_URL}"
echo "    GOOGLE_MAPS_KEY=$([ -n "${GOOGLE_MAPS_KEY}" ] && echo set || echo missing)"
flutter pub get

case "$TARGET" in
  android|aab|appbundle)
    flutter build appbundle --release "${DEFINES[@]}"
    echo "AAB: build/app/outputs/bundle/release/app-release.aab"
    ;;
  apk)
    flutter build apk --release "${DEFINES[@]}"
    echo "APK: build/app/outputs/flutter-apk/app-release.apk"
    ;;
  ios|ipa)
    flutter build ipa --release "${DEFINES[@]}"
    echo "IPA under build/ios/ipa/"
    ;;
  *)
    echo "Unknown target: $TARGET (use android|apk|ios)"
    exit 1
    ;;
esac
