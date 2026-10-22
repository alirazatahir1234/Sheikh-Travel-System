#!/usr/bin/env bash
# Install Caddy on SheikhGo Azure VM and enable HTTPS reverse-proxy → :5082.
# Safe to re-run. Does not stop Kestrel HTTP on :5082 (mobile may still use it).
set -euo pipefail

if [[ "$(id -u)" -eq 0 ]]; then
  echo "Run as azureuser (script uses sudo)." >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CADDYFILE_SRC="${CADDYFILE_SRC:-$SCRIPT_DIR/Caddyfile}"
EMAIL="${CADDY_ACME_EMAIL:-info@sheikhgo.com}"

if [[ ! -f "$CADDYFILE_SRC" ]]; then
  echo "Missing Caddyfile at $CADDYFILE_SRC" >&2
  exit 1
fi

echo "==> Installing Caddy (official apt repo) if needed"
if ! command -v caddy >/dev/null 2>&1; then
  sudo apt-get update -y
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y \
    debian-keyring debian-archive-keyring apt-transport-https curl
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' \
    | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
    | sudo tee /etc/apt/sources.list.d/caddy-stable.list >/dev/null
  sudo apt-get update -y
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y caddy
fi

caddy version

echo "==> Installing Caddyfile"
sudo mkdir -p /etc/caddy /var/log/caddy
sudo chown caddy:caddy /var/log/caddy

# Fresh file: global ACME email + site block from repo
{
  printf '%s\n' '{' "	email ${EMAIL}" '}' ''
  # Drop a leading global block from the source if present
  awk '
    BEGIN { skip=0 }
    NR==1 && $0 == "{" { skip=1; next }
    skip && $0 == "}" { skip=0; next }
    skip { next }
    { print }
  ' "$CADDYFILE_SRC"
} | sudo tee /etc/caddy/Caddyfile >/dev/null

sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl enable caddy
sudo systemctl restart caddy
sleep 2
sudo systemctl is-active caddy

echo "==> Waiting for HTTPS (sslip.io works without custom DNS)"
for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18; do
  sleep 5
  CODE="$(curl -sS -m 25 -o /tmp/caddy-health.txt -w '%{http_code}' https://20-174-1-230.sslip.io/health || true)"
  BODY="$(cat /tmp/caddy-health.txt 2>/dev/null || true)"
  echo "  try=$i => HTTP $CODE body=$BODY"
  if [[ "$CODE" == "200" && "$BODY" == "Healthy" ]]; then
    echo "==> HTTPS OK: https://20-174-1-230.sslip.io/health"
    echo "    Preferred host (after DNS A record): https://api.sheikhgo.com/health"
    exit 0
  fi
done

echo "HTTPS health check did not pass yet. Check: journalctl -u caddy -n 80 --no-pager" >&2
sudo journalctl -u caddy -n 80 --no-pager || true
exit 1
