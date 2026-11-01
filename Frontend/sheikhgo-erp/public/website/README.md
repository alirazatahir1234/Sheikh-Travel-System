# Optional marketing media for the public homepage

**Production serving:** Cloudflare Worker CDN → Azure Blob (`sheikhtravelblob`).  
App base: `environment.websiteMediaBaseUrl` = `https://sheikhgo-media.sheikhgo.workers.dev` (no apex/www DNS change).

| Kind | Container | Example URL |
|------|-----------|-------------|
| Images / captions | `website-images` | `https://sheikhgo-media.sheikhgo.workers.dev/website-images/gps-alerts/hero-bg.jpg` |
| Videos | `website-videos` | `https://sheikhgo-media.sheikhgo.workers.dev/website-videos/demo/sheikhgo-nexus-promo-120s.mp4` |

Angular resolves logical `/website/...` paths via `websiteAssetUrl()` / `environment.websiteMediaBaseUrl`.
To retarget CDN host later (e.g. `media.sheikhgo.com`), change only `websiteMediaBaseUrl` + `$sg-website-images-base` (see `docs/deploy/cloudflare-media-cdn.md`).

## Upload convention (new site media)

1. Place the final asset under `public/website/...` (keep folder layout: `demo/`, `nexus/modules/`, `gps-alerts/`, `product/`).
2. Do **not** commit or upload intermediates: `demo/_segments/`, `demo/_erp_segments/`, `demo/promo-production/`, `demo/mobile-promo-production/`.
3. Do **not** put GPS playbacks or private tenant data in these public containers.
4. Upload with:

```bash
cd Frontend/sheikhgo-erp
./scripts/upload-website-media.sh
```

5. Reference in code as `/website/...` (helper maps to the correct container).

## Hero / screenshots / demos

- `sheikhgo-fleet-hero.png` — poster / fallback still
- `hero-demo.mp4` — optional homepage hero loop (gitignored locally; on Blob)
- `privacy-policy-banner.jpg` — Privacy Policy page hero banner
- `demo/sheikhgo-nexus-erp-walkthrough.mp4` — Watch Demo primary (prod prefers `/website-demo/erp-walkthrough` stream)
- `demo/sheikhgo-nexus-promo-120s.mp4` — Watch Promo (+ `.srt` / `.vtt`)
- `demo/sheikhgo-nexus-walkthrough.mp4` — marketing site + ERP montage
- `demo/sheikhgo-nexus-mobile-promo.mp4` — mobile promo (+ poster / captions)

Record SheikhGo Nexus with a demo tenant only (no real plates, drivers, or customer GPS).
