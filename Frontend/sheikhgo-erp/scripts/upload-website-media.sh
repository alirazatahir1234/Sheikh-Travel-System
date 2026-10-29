#!/usr/bin/env bash
# Upload marketing media from public/website to Azure Blob (sheikhtravelblob).
# Skips demo intermediates (_segments, promo-production, etc.).
# Usage (from Frontend/sheikhgo-erp):
#   ./scripts/upload-website-media.sh
# Requires: az login, Contributor/Storage Blob Data rights on sheikhtravelblob.
set -euo pipefail

ACCT="${AZURE_STORAGE_ACCOUNT:-sheikhtravelblob}"
RG="${AZURE_STORAGE_RG:-NetworkWatcherRG}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)/public/website"
KEY="$(az storage account keys list -n "$ACCT" -g "$RG" --query '[0].value' -o tsv)"

upload_one() {
  local file="$1" container="$2" blob="$3" ct="$4"
  echo "→ ${container}/${blob}"
  az storage blob upload \
    --account-name "$ACCT" --account-key "$KEY" \
    --container-name "$container" \
    --file "$file" --name "$blob" \
    --content-type "$ct" --overwrite true --only-show-errors -o none
}

cd "$ROOT"
while IFS= read -r -d '' file; do
  rel="${file#./}"
  ext="${rel##*.}"
  case "$ext" in
    jpg|jpeg) ct="image/jpeg"; container="website-images" ;;
    png) ct="image/png"; container="website-images" ;;
    webp) ct="image/webp"; container="website-images" ;;
    svg) ct="image/svg+xml"; container="website-images" ;;
    vtt) ct="text/vtt"; container="website-images" ;;
    srt) ct="application/x-subrip"; container="website-images" ;;
    mp4) ct="video/mp4"; container="website-videos" ;;
    webm) ct="video/webm"; container="website-videos" ;;
    *) continue ;;
  esac
  upload_one "$file" "$container" "$rel" "$ct"
done < <(find . -type f \
  \( -path './demo/_*' -o -path './demo/promo-production/*' -o -path './demo/mobile-promo-production/*' \) -prune -o \
  -type f \( \
    -iname '*.png' -o -iname '*.jpg' -o -iname '*.jpeg' -o -iname '*.webp' -o -iname '*.svg' \
    -o -iname '*.mp4' -o -iname '*.webm' -o -iname '*.vtt' -o -iname '*.srt' \
  \) -print0)

# Optional public logo for OG / SEO
LOGO="$(cd "$(dirname "$0")/.." && pwd)/public/brand/sheikhgo-logo.png"
if [[ -f "$LOGO" ]]; then
  upload_one "$LOGO" website-images "brand/sheikhgo-logo.png" "image/png"
fi

unset KEY
echo "Done. Base: https://${ACCT}.blob.core.windows.net/{website-images|website-videos}/..."
