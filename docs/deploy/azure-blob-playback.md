# Azure Blob — GPS playback coordinates (private + SAS)

## Policy

GPS / history / playback **coordinate files** (GPX, GeoJSON, KML, CSV, JSON archives) stored in Azure Blob must be served via **time-limited signed URLs (SAS) only**. Never enable anonymous public access on the playback container.

Live map / trip **replay in the ERP** continues to use authenticated API JSON (`/gps/history/replay`, `/gps/trips/replay`) from SQL/Traccar — those paths do not read public blobs.

## Account & containers (`sheikhtravelblob`)

| Container | ACL | Purpose |
|-----------|-----|---------|
| `gps-playbacks` | **Private** (`publicAccess` null/off) | Playback & history coordinate files |
| `sheikhtravelcontainer` | Private | General ERP uploads (vehicles, drivers, …) |
| `website-images` | Public blob (marketing) | Website images only |
| `website-videos` | Public blob (marketing) | Website videos only |

**Do not** set `gps-playbacks` (or `sheikhtravelcontainer`) to public when enabling public access for website media. Account-level `allowBlobPublicAccess=true` is required for website containers but **container ACL** on `gps-playbacks` must stay off.

## Config (`FileStorage`)

```json
"FileStorage": {
  "AzureContainerName": "sheikhtravelcontainer",
  "AzurePublicBaseUrl": "",
  "AzureSasExpiryDays": 365,
  "PlaybackContainerName": "gps-playbacks",
  "PlaybackSasExpiryMinutes": 60
}
```

- Playback SAS TTL: **`PlaybackSasExpiryMinutes`** (default **60**, clamped 5–1440).
- Playback code **never** uses `AzurePublicBaseUrl` / CDN for coordinate blobs.
- Website marketing base URLs use Cloudflare Worker CDN for `website-images` / `website-videos` only (`websiteMediaBaseUrl` → `https://sheikhgo-media.sheikhgo.workers.dev`; see [cloudflare-media-cdn.md](./cloudflare-media-cdn.md)).
- Public media CDN (Cloudflare Worker, not Azure CDN): [cloudflare-media-cdn.md](./cloudflare-media-cdn.md). Never put `gps-playbacks` on that CDN.

## Code paths

| Path | Behavior |
|------|----------|
| `IPlaybackCoordinateStorage` | Dedicated storage; Azure impl creates container with `PublicAccessType.None` and issues blob read SAS |
| `AzurePlaybackCoordinateStorage` | Account `sheikhtravelblob` → container `gps-playbacks` |
| `GET /api/gps/history/{vehicleId}/export?delivery=file` | Authenticated API file download (default; no blob required) |
| `GET /api/gps/history/{vehicleId}/export?delivery=signed` | Stores under `gps-playbacks` + returns SAS JSON (`HistoryExportSignedUrlDto`) |
| `export?persist=true` | Also writes to private playback container when using `delivery=file` |
| Future archival / cold storage | Call `IPlaybackCoordinateStorage.SaveAsync` / `ResolveSignedReadUrl` only |

Frontend helper: `GpsTrackingService.exportHistorySignedUrl(...)`.

## Ops checklist

1. Confirm container: `az storage container show -n gps-playbacks --account-name sheikhtravelblob --query properties.publicAccess` → empty/null.
2. Never run `set-permission --public-access blob` on `gps-playbacks`.
3. Rotate / avoid committing storage keys; connection string via user secrets / env only.
