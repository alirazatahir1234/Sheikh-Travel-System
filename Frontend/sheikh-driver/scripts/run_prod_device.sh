#!/usr/bin/env bash
# Build a release APK pointed at Production HTTPS (Railway) for physical devices.
# Install anywhere with internet — no Mac IP / local API required.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

exec ./scripts/build_prod.sh apk prod
