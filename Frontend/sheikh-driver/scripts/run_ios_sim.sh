#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
open -a Simulator
flutter run -d "iPhone 17 Pro" \
  --dart-define=API_BASE_URL=http://127.0.0.1:5082/api \
  --dart-define=ENV=dev
