#!/usr/bin/env bash
# Run from repo root: ./Frontend/sheikhgo-driver/setup.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

if ! command -v flutter >/dev/null 2>&1; then
  echo "Flutter is not installed or not on PATH."
  echo ""
  echo "Install (macOS, Homebrew):"
  echo "  brew install --cask flutter"
  echo ""
  echo "Then add to ~/.zshrc:"
  echo '  export PATH="$PATH:/opt/homebrew/Caskroom/flutter/latest/flutter/bin"'
  echo ""
  echo "Or download: https://docs.flutter.dev/get-started/install/macos"
  exit 1
fi

echo "Using: $(which flutter)"
flutter --version

# Generate android/, ios/, etc. without overwriting lib/
if [ ! -d android ]; then
  flutter create . --project-name sheikh_driver
fi

flutter pub get
echo ""
echo "Run the app (API must be up on port 5082):"
echo "  flutter run --dart-define=API_BASE_URL=http://127.0.0.1:5082/api"
