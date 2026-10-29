#!/usr/bin/env bash
# Build + deploy Sheikh Travel API on SheikhGo Azure VM (systemd).
# Safe to run from Azure Pipelines self-hosted agent or manually via SSH.
# Does NOT touch Traccar, SQL Server, or Docker containers other than reading env.
set -euo pipefail

APP_ROOT="${APP_ROOT:-/opt/sheikhgo-api}"
SRC_DIR="${SRC_DIR:-$APP_ROOT/src}"
RELEASES_DIR="$APP_ROOT/releases"
SERVICE_NAME="${SERVICE_NAME:-sheikhgo-api}"
BRANCH="${DEPLOY_BRANCH:-main}"
KEEP_RELEASES="${KEEP_RELEASES:-5}"

if [[ "$(id -u)" -eq 0 ]]; then
  echo "Do not run as root; run as azureuser (systemd User=azureuser)." >&2
  exit 1
fi

echo "==> Deploying SheikhGo API (branch=$BRANCH)"

if [[ -d "$SRC_DIR/.git" ]]; then
  cd "$SRC_DIR"
  # When Azure Pipelines already checked out this workspace, skip reset if BUILD_SOURCESDIRECTORY matches
  if [[ -n "${BUILD_SOURCESDIRECTORY:-}" && -d "${BUILD_SOURCESDIRECTORY}/Backend" ]]; then
    SRC_DIR="$BUILD_SOURCESDIRECTORY"
    echo "==> Using pipeline checkout: $SRC_DIR"
  else
    echo "==> git fetch/reset $BRANCH"
    git fetch --depth 1 origin "$BRANCH"
    git checkout -B "$BRANCH" "origin/$BRANCH"
  fi
else
  echo "Missing git clone at $SRC_DIR" >&2
  exit 1
fi

cd "$SRC_DIR/Backend/SheikhTravelSystem.API"
STAMP="$(date -u +%Y%m%d%H%M%S)"
OUT="$RELEASES_DIR/$STAMP"
mkdir -p "$OUT"

echo "==> dotnet publish -> $OUT"
dotnet publish SheikhTravelSystem.API.csproj -c Release -r linux-x64 --self-contained false -o "$OUT"

test -f "$OUT/SheikhTravelSystem.API.dll"

ln -sfn "$OUT" "$APP_ROOT/current"
echo "==> current -> $OUT"

# Prune old releases
cd "$RELEASES_DIR"
ls -1dt */ 2>/dev/null | tail -n +"$((KEEP_RELEASES + 1))" | xargs -r rm -rf

echo "==> systemctl restart $SERVICE_NAME"
sudo systemctl restart "$SERVICE_NAME"
sudo systemctl is-active "$SERVICE_NAME"

HTTP=""; BODY=""
health_ok() {
  # Accept legacy plain "Healthy" or JSON {"status":"Healthy",...}.
  [[ "$HTTP" == "200" ]] || return 1
  [[ "$BODY" == "Healthy" ]] && return 0
  echo "$BODY" | grep -Eqi '"status"[[:space:]]*:[[:space:]]*"Healthy"'
}
for i in 1 2 3 4 5 6 7 8 9 10; do
  sleep 3
  HTTP="$(curl -s -m 20 -o /tmp/sheikhgo-health.txt -w '%{http_code}' http://127.0.0.1:5082/health || true)"
  BODY="$(cat /tmp/sheikhgo-health.txt 2>/dev/null || true)"
  echo "==> /health try=$i => HTTP $HTTP body=$BODY"
  if health_ok; then
    break
  fi
done
if ! health_ok; then
  echo "Health check failed" >&2
  journalctl -u "$SERVICE_NAME" -n 40 --no-pager || true
  exit 1
fi

echo "==> Deploy OK"
free -h | head -3
