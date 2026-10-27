# SheikhGo Nexus 120s Promo — Production Report

**Deliverable:** `../sheikhgo-nexus-promo-120s.mp4`  
**Subtitles:** `../sheikhgo-nexus-promo-120s.srt` + `.vtt`  
**Copy bank:** `VIDEO-COPY-BANK.md` (pointer: `copy-bank.md`)  
**Script:** `script.md` (12-scene brief — *Smarter Fleet Management*)  
**Duration:** 120.00s · **1280×720** · H.264 + AAC  

## Re-render notes (2026-10-27/28)

| Change | Detail |
|--------|--------|
| Color | Brighter `eq` grade (slight lift, mild contrast/sat) |
| Overlays | Light text boxes (alpha ~58) so UI stays readable |
| Motion | **Stable stills** — Ken Burns / zoompan / shake removed |
| VO weave | §A Eco Nexus on scenes 1–2; §B SheikhGo Nexus for 3–12 |
| CTA | https://www.sheikhgo.com · Book a Demo (unchanged) |

## Pipeline

| Layer | Source |
|--------|--------|
| Visuals | Stable stills from `public/website/` Nexus module PNGs/JPGs + brand logo |
| On-screen text | PIL PNG overlays (header, scene title, Eco/compliance subtitles) |
| Watermark | `public/brand/sheikhgo-logo-white.png` (upper-right) |
| VO | `edge-tts` · `en-US-GuyNeural` · fit via `atempo` + pad (retries + reuse) |
| Music | `docs/demo/audio-src/mixkit-motivating-mornings-33.mp3` @ ~14% under VO |
| Assembly | `build_promo.py` → ffmpeg concat + amix |

## Scene map

| # | Time | Title / VO note |
|---|------|-----------------|
| 1 | 0:00–0:08 | Opening — **Eco Nexus** workplace line (§A) |
| 2 | 0:08–0:20 | Real-time GPS — **Track every vehicle real-time** (§A) |
| 3–11 | … | SheikhGo Nexus §B VO |
| 12 | 1:50–2:00 | CTA — sheikhgo.com · Book a Demo |

## Compliance

- No fabricated stats or third-party customer logos.
- AI scene labeled sample demo data; Ollama/DeepInfra for production tenants.
- Fuel: *suspected anomalies / investigate*.
- SOS: **no guaranteed ETAs**.
- CTA URL: https://www.sheikhgo.com only.
- Eco Nexus limited to Ali §A lines only.

## Regenerate

```bash
cd Frontend/sheikhgo-erp/public/website/demo/promo-production
PROMO_FFMPEG_THREADS=1 python3 build_promo.py
# resume: PROMO_START_SCENE=9 python3 build_promo.py
```
