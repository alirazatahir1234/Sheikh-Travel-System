# Mobile promo — production report

**Date:** 2026-10-27  
**Output:** `sheikhgo-nexus-mobile-promo.mp4` (~54s, 720×1560) + `.vtt` / `.srt`  
**Method:** Offline edit from `_segments/ios-walkthrough.mov` (API `:5082` OOM — no re-record)

## Claims (evidence-backed)

| Beat | On-screen / VO | Evidence |
|------|----------------|----------|
| Splash | Eco Nexus · intelligent workplace | App splash + Ali copy bank §A |
| Login | Owners / drivers | Login hero copy in capture |
| Dashboard | Fleet command | Owner tracking dashboard |
| Live Map | Track every vehicle real-time | Live Map tab + parked marker |
| More | Vehicles · Trips · role shell | More grid tiles + bottom nav |
| Close | Book a Demo · sheikhgo.com | CTA only |

**Not claimed:** EV fleet, SOS partners/ETAs, ratings, AI assistant (not in this capture).

## Site wiring

- `demo-media.ts`: `NEXUS_MOBILE_PROMO_*`, mode `'mobile'`
- Watch Demo modal tab **Mobile App**
- Captions track when mode is mobile

## Rebuild

```bash
cd Frontend/sheikhgo-erp/public/website/demo/mobile-promo-production
PROMO_FFMPEG_THREADS=1 python3 build_mobile_promo.py
```

Re-record (when API has RAM): `Frontend/sheikh-driver/tools/record-ios-mobile-demo.sh` then rebuild.
