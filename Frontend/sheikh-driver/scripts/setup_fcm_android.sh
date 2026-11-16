#!/usr/bin/env bash
# Wire Firebase Cloud Messaging for SheikhGo Tracking (Android).
#
# Usage:
#   1) Download google-services.json from Firebase Console for package
#      com.sheikhgo.fleet, OR run this after `firebase login`.
#   2) ./scripts/setup_fcm_android.sh [/path/to/google-services.json]
#   3) Rebuild Play AAB: ./scripts/build_prod.sh android
#
# This script never invents Firebase keys.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="$ROOT/android/app/google-services.json"
PACKAGE='com.sheikhgo.fleet'
SRC="${1:-}"

echo "==> SheikhGo FCM Android setup"
echo "    expected package: $PACKAGE"
echo "    destination: $DEST"

if [[ -n "$SRC" ]]; then
  if [[ ! -f "$SRC" ]]; then
    echo "ERROR: file not found: $SRC"
    exit 1
  fi
  cp -f "$SRC" "$DEST"
  echo "    copied from $SRC"
elif [[ -f "$DEST" ]]; then
  echo "    existing google-services.json found"
else
  # Try Firebase CLI if authenticated and FIREBASE_APP_ID is set.
  if command -v firebase >/dev/null 2>&1 && [[ -n "${FIREBASE_ANDROID_APP_ID:-}" ]]; then
    echo "    fetching via firebase apps:sdkconfig ..."
    firebase apps:sdkconfig ANDROID "$FIREBASE_ANDROID_APP_ID" >"$DEST"
  else
    cat <<EOF

ERROR: android/app/google-services.json is missing.

Do this once (Firebase Console):
  1. Open https://console.firebase.google.com/
  2. Select/create project (e.g. SheikhGo)
  3. Add Android app with package name: $PACKAGE
  4. Download google-services.json
  5. Re-run:
       ./scripts/setup_fcm_android.sh ~/Downloads/google-services.json
  6. Rebuild:
       ./scripts/build_prod.sh android

Optional CLI path (after firebase login):
  FIREBASE_ANDROID_APP_ID=1:xxxx:android:yyyy ./scripts/setup_fcm_android.sh

EOF
    exit 2
  fi
fi

# Validate package name without dumping secrets.
python3 - <<'PY' "$DEST" "$PACKAGE"
import json, sys
path, pkg = sys.argv[1], sys.argv[2]
with open(path) as f:
    data = json.load(f)
clients = data.get("client") or []
packages = []
for c in clients:
    info = (c.get("client_info") or {}).get("android_client_info") or {}
    p = info.get("package_name")
    if p:
        packages.append(p)
project = (data.get("project_info") or {}).get("project_id") or "?"
if pkg not in packages:
    print(f"ERROR: google-services.json has packages {packages or ['(none)']}; expected {pkg}")
    sys.exit(3)
print(f"OK: project_id={project} package={pkg}")
PY

echo ""
echo "Next:"
echo "  • Enable Cloud Messaging API in Google Cloud for this Firebase project"
echo "  • Backend: set Notifications:Push:Enabled=true, ProjectId, CredentialsPath"
echo "    (service-account JSON with roles/firebase.messaging / Firebase Cloud Messaging Admin)"
echo "  • Rebuild Play upload:"
echo "      cd \"$ROOT\" && ./scripts/build_prod.sh android"
