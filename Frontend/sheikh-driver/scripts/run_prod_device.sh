#!/usr/bin/env bash
# Build a release APK pointed at Production HTTPS (sslip.io) for physical devices.
# Install anywhere with internet — no Mac IP / local API required.
# Emulators: use ./scripts/run_android_emu.sh (debug) — release blocks emulators.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

exec ./scripts/build_prod.sh apk prod
