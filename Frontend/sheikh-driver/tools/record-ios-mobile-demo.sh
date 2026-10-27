#!/usr/bin/env bash
# Records an iOS Simulator walkthrough of SheikhGo Fleet (sheikh-driver) to MP4.
#
# Prereqs: Xcode Simulator, Flutter, ffmpeg on PATH.
#
# Usage (from repo root or Frontend/sheikh-driver):
#   ./tools/record-ios-mobile-demo.sh
#   IOS_SIM_UDID=... OUT=../../docs/demo ./tools/record-ios-mobile-demo.sh
set -euo pipefail
cd "$(dirname "$0")/.."

SIMCTL="/Applications/Xcode.app/Contents/Developer/usr/bin/simctl"
DEVICE="${IOS_SIM_UDID:-45B711C1-5A22-4021-9619-04B9834FCC38}"
ERP_DEMO="../sheikhgo-erp/public/website/demo"
OUT_DIR="${OUT:-$ERP_DEMO}"
SEG_DIR="$OUT_DIR/_segments"
MOV="$SEG_DIR/ios-walkthrough.mov"
MP4="$OUT_DIR/sheikhgo-nexus-ios-walkthrough.mp4"
MANIFEST="$OUT_DIR/sheikhgo-nexus-ios-walkthrough.manifest.json"

mkdir -p "$SEG_DIR"
open -a Simulator || true
"$SIMCTL" boot "$DEVICE" 2>/dev/null || true

echo "Recording on $DEVICE → $MOV"
"$SIMCTL" io "$DEVICE" recordVideo "$MOV" &
REC_PID=$!

cleanup() {
  if kill -0 "$REC_PID" 2>/dev/null; then
    kill -INT "$REC_PID" || true
    wait "$REC_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

ENV_DEFINE="${ENV:-dev}"
API_DEFINE="${API_BASE_URL:-http://127.0.0.1:5082/api}"

set +e
flutter test integration_test/demo_walkthrough_test.dart \
  -d "$DEVICE" \
  --dart-define=ENV="$ENV_DEFINE" \
  --dart-define=API_BASE_URL="$API_DEFINE" \
  --dart-define=DEMO_EMAIL="${DEMO_EMAIL:-admin@sheikhtravel.com}" \
  --dart-define=DEMO_PASSWORD="${DEMO_PASSWORD:-Pass@123}"
TEST_EXIT=$?
set -e
if [[ "$TEST_EXIT" -ne 0 ]]; then
  echo "Note: integration test exited $TEST_EXIT (video may still be usable)."
fi

sleep 1
cleanup
trap - EXIT

if ! command -v ffmpeg >/dev/null 2>&1; then
  echo "ffmpeg not found; leaving MOV at $MOV"
  exit 0
fi

ffmpeg -y -i "$MOV" -c:v libx264 -pix_fmt yuv420p -movflags +faststart "$MP4"
echo "Wrote $MP4"

cat > "$MANIFEST" <<EOF
{
  "title": "SheikhGo Nexus iOS mobile walkthrough",
  "app": "Frontend/sheikh-driver",
  "bundleId": "com.sheikhgo.fleet",
  "device": "$DEVICE",
  "api": "ENV=$ENV_DEFINE API_BASE_URL=$API_DEFINE",
  "video": "$(basename "$MP4")",
  "recordedAt": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")",
  "script": "docs/demo/SheikhGo-Nexus-iOS-Walkthrough-Script.md"
}
EOF
