# Facebook marketing media — CDN (cloud)

Local binaries under `docs/marketing/facebook/` were uploaded to Azure public containers and verified via Cloudflare Worker, then removed locally.

**CDN base:** `https://sheikhgo-media.sheikhgo.workers.dev`  
**Classification:** all public website/marketing → `website-images` / `website-videos` (not private `sheikhtravelcontainer`).

Download from CDN when posting (do not commit large binaries back into git).

## Product JPGs (`website-images`)

| File | CDN URL |
|------|---------|
| sheikhgo-tracker-pricing-20k-40k.jpg | https://sheikhgo-media.sheikhgo.workers.dev/website-images/marketing/facebook/sheikhgo-tracker-pricing-20k-40k.jpg |
| sheikhgo-tracker-20k-pricing.jpg | https://sheikhgo-media.sheikhgo.workers.dev/website-images/marketing/facebook/sheikhgo-tracker-20k-pricing.jpg |
| sheikhgo-fb-product-ad-square.jpg | https://sheikhgo-media.sheikhgo.workers.dev/website-images/marketing/facebook/sheikhgo-fb-product-ad-square.jpg |
| sheikhgo-fb-product-photo-vertical.jpg | https://sheikhgo-media.sheikhgo.workers.dev/website-images/marketing/facebook/sheikhgo-fb-product-photo-vertical.jpg |
| sheikhgo-fleet-reimagined-social.jpg | https://sheikhgo-media.sheikhgo.workers.dev/website-images/marketing/facebook/sheikhgo-fleet-reimagined-social.jpg |
| reel_frames/frame_app_01.jpg | https://sheikhgo-media.sheikhgo.workers.dev/website-images/marketing/facebook/reel_frames/frame_app_01.jpg |
| reel_frames/frame_app_02.jpg | https://sheikhgo-media.sheikhgo.workers.dev/website-images/marketing/facebook/reel_frames/frame_app_02.jpg |
| reel_frames/frame_app_03.jpg | https://sheikhgo-media.sheikhgo.workers.dev/website-images/marketing/facebook/reel_frames/frame_app_03.jpg |

## Logos / overlays PNG (`website-images`)

| File | CDN URL |
|------|---------|
| sheikhgo-logo.png | https://sheikhgo-media.sheikhgo.workers.dev/website-images/marketing/facebook/sheikhgo-logo.png |
| sheikhgo-nexus-logo.png | https://sheikhgo-media.sheikhgo.workers.dev/website-images/marketing/facebook/sheikhgo-nexus-logo.png |
| reel_overlays/overlay_01.png … overlay_05.png | `…/website-images/marketing/facebook/reel_overlays/overlay_0N.png` |
| reel_overlays/overlay_app_*.png | `…/website-images/marketing/facebook/reel_overlays/overlay_app_{dash,details,trips}.png` |

## Videos (`website-videos`)

| File | CDN URL |
|------|---------|
| sheikhgo-english-reel-narowal-lahore.mp4 | https://sheikhgo-media.sheikhgo.workers.dev/website-videos/marketing/facebook/sheikhgo-english-reel-narowal-lahore.mp4 |
| sheikhgo-fb-demo-reel.mp4 | https://sheikhgo-media.sheikhgo.workers.dev/website-videos/marketing/facebook/sheikhgo-fb-demo-reel.mp4 |

## Local status

Media binaries: **cleared** after CDN HEAD `200`. Caption markdown kept in this folder.
