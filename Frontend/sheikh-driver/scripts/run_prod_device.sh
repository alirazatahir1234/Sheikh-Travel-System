#!/usr/bin/env bash
# Build a release APK pointed at Production API (Azure VM :5082, same as Fleet)
# for physical devices. Install anywhere with internet — no Mac IP / local API.

# Emulators: use ./scripts/run_android_emu.sh (debug) — release blocks emulators.
# iPhone USB release: ./scripts/run_ios_prod_device.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

exec ./scripts/build_prod.sh apk prod
