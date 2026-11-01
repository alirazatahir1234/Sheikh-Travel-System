# ERP demo video — web protection (best effort)

**Watch Demo** plays from `/website-demo/erp-walkthrough` (Vercel serverless stream with HTTP Range support). The file remains at `public/website/demo/sheikhgo-nexus-erp-walkthrough.mp4` for recording tools; production static output strips that MP4 so it is not served as a plain CDN asset.

## What we enforce

- No public download links in the demo modal; `controlsList` includes `nodownload`.
- Context menu blocked on the player surface.
- Legacy URL `/website/demo/sheikhgo-nexus-erp-walkthrough.mp4` rewrites to the same stream handler (not a static file).
- **Download** (`?download=1`) requires a valid Bearer token and an allowlisted email (`DEMO_VIDEO_DOWNLOAD_EMAILS`, default `info@sheikhgo.com`) or `SUPER_ADMIN` from `GET /api/auth/me`.

## Limitations

This is not DRM. A determined user can still capture the stream (devtools Network tab, screen recording, etc.). True studio-grade protection needs a DRM provider (Widevine/FairPlay) or private blob storage with short-lived tokens per segment.

## Ops

| Variable | Purpose |
|----------|---------|
| `DEMO_VIDEO_DOWNLOAD_EMAILS` | Comma-separated emails allowed to download (Vercel env) |
| `SHEIKHGO_API_URL` | API base for auth check (default: Cloudflare quick tunnel `/api`) |

Local `ng serve` uses the static MP4 path in dev only (`environment.production === false`).
