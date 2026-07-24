#!/usr/bin/env bash
# Production / UAT release builds for SheikhGo Fleet.
# Usage:
#   ./scripts/build_prod.sh android
#   ./scripts/build_prod.sh ios
#   ./scripts/build_prod.sh android uat
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

TARGET="${1:-android}"
ENV_NAME="${2:-prod}"

: "${API_BASE_URL:?Set API_BASE_URL to your API root, e.g. https://api.example.com/api}"

DEFINES=(
  "--dart-define=ENV=${ENV_NAME}"
  "--dart-define=API_BASE_URL=${API_BASE_URL}"
  "--dart-define=TENANT_SLUG=${TENANT_SLUG:-default}"
)

if [[ -n "${GOOGLE_MAPS_KEY:-}" ]]; then
  DEFINES+=("--dart-define=GOOGLE_MAPS_KEY=${GOOGLE_MAPS_KEY}")
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

echo "==> SheikhGo Fleet ${ENV_NAME} build (${TARGET})"
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
