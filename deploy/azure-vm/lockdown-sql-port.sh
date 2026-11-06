#!/usr/bin/env bash
# Recreate sheikhgo-sql so host port 1433 binds to 127.0.0.1 only (not 0.0.0.0).
# Safe for SheikhGo API: connection strings use localhost / 127.0.0.1,1433.
# Does NOT change SA password, volumes, or databases.
set -euo pipefail

CONTAINER="${SQL_CONTAINER:-sheikhgo-sql}"
IMAGE="${SQL_IMAGE:-mcr.microsoft.com/mssql/server:2022-latest}"
HOST_BIND="${SQL_HOST_BIND:-127.0.0.1}"
HOST_PORT="${SQL_HOST_PORT:-1433}"
CONTAINER_PORT="${SQL_CONTAINER_PORT:-1433}"

if [[ "$(id -u)" -eq 0 ]]; then
  echo "Do not run as root; use azureuser + sudo docker." >&2
  exit 1
fi

if ! sudo docker inspect "$CONTAINER" >/dev/null 2>&1; then
  echo "Container $CONTAINER not found." >&2
  exit 1
fi

BEFORE="$(sudo docker port "$CONTAINER" 2>/dev/null || true)"
echo "==> Before publish: $BEFORE"

# Preserve all mounts (data volume + optional backups bind, etc.)
VOL_ARGS=()
has_data_vol=0
while IFS= read -r line; do
  [[ -z "$line" ]] && continue
  VOL_ARGS+=(-v "$line")
  [[ "$line" == *":/var/opt/mssql" && "$line" != *":/var/opt/mssql/"* ]] && has_data_vol=1
done < <(sudo docker inspect "$CONTAINER" --format '{{range .Mounts}}{{if .Name}}{{.Name}}{{else}}{{.Source}}{{end}}:{{.Destination}}{{println}}{{end}}')

if [[ "$has_data_vol" -ne 1 ]]; then
  echo "No /var/opt/mssql mount found on $CONTAINER — aborting to avoid data loss." >&2
  exit 1
fi

# Copy env (including MSSQL_SA_PASSWORD) without printing secrets
ENV_ARGS=()
while IFS= read -r e; do
  [[ -z "$e" ]] && continue
  ENV_ARGS+=(-e "$e")
done < <(sudo docker inspect "$CONTAINER" --format '{{range .Config.Env}}{{println .}}{{end}}')

RESTART="$(sudo docker inspect "$CONTAINER" --format '{{.HostConfig.RestartPolicy.Name}}')"
[[ -z "$RESTART" || "$RESTART" == "no" ]] && RESTART="unless-stopped"

BACKUP="${CONTAINER}-pre-lockdown-$(date -u +%Y%m%d%H%M%S)"
echo "==> Stopping and renaming $CONTAINER -> $BACKUP"
sudo docker stop "$CONTAINER"
sudo docker rename "$CONTAINER" "$BACKUP"

echo "==> Creating $CONTAINER with ${HOST_BIND}:${HOST_PORT}:${CONTAINER_PORT}"
sudo docker run -d \
  --name "$CONTAINER" \
  --restart "$RESTART" \
  -p "${HOST_BIND}:${HOST_PORT}:${CONTAINER_PORT}" \
  "${VOL_ARGS[@]}" \
  "${ENV_ARGS[@]}" \
  "$IMAGE"

# Wait for TCP on localhost, then for API SQL health (MSSQL can accept TCP before ready)
ok=0
for i in 1 2 3 4 5 6 7 8 9 10 11 12; do
  sleep 5
  if timeout 3 bash -c "echo >/dev/tcp/127.0.0.1/${HOST_PORT}" 2>/dev/null; then
    ok=1
    echo "==> localhost:${HOST_PORT} accepting (try=$i)"
    break
  fi
  echo "==> waiting for SQL try=$i"
done
if [[ "$ok" -ne 1 ]]; then
  echo "SQL did not come up on localhost — rolling back rename." >&2
  sudo docker stop "$CONTAINER" || true
  sudo docker rm "$CONTAINER" || true
  sudo docker rename "$BACKUP" "$CONTAINER"
  sudo docker start "$CONTAINER"
  exit 1
fi

AFTER="$(sudo docker port "$CONTAINER" 2>/dev/null || true)"
echo "==> After publish: $AFTER"
if echo "$AFTER" | grep -q '0.0.0.0'; then
  echo "ERROR: still bound to 0.0.0.0" >&2
  exit 1
fi

sql_healthy=0
for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18; do
  sleep 5
  DETAIL="$(curl -sS -m 20 http://127.0.0.1:5082/health/detail 2>/dev/null || true)"
  HTTP="$(curl -s -m 15 -o /tmp/sheikhgo-sql-lockdown-health.txt -w '%{http_code}' http://127.0.0.1:5082/health || true)"
  BODY="$(cat /tmp/sheikhgo-sql-lockdown-health.txt 2>/dev/null || true)"
  echo "==> /health try=$i => HTTP $HTTP body=$BODY"
  if echo "$DETAIL" | grep -Eqi '"sql"[[:space:]]*:[[:space:]]*\{[^}]*"status"[[:space:]]*:[[:space:]]*"Healthy"'; then
    sql_healthy=1
    echo "$DETAIL" | head -c 1500 || true
    echo
    break
  fi
done
if [[ "$sql_healthy" -ne 1 ]]; then
  echo "WARN: API sql health not Healthy yet; container bind is locked down. Check /health/detail." >&2
fi

echo "==> Old container kept as $BACKUP (docker rm $BACKUP when satisfied)"
echo "==> Lockdown OK"
