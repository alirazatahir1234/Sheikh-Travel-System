# SheikhGo Nexus 120s Promo — Production Report

**Deliverable:** `../sheikhgo-nexus-promo-120s.mp4`  
**Subtitles:** `../sheikhgo-nexus-promo-120s.srt`  
**Script:** `script.md` (12-scene brief — *Smarter Fleet Management*)  
**Duration:** 120.00s · **1280×720** · H.264 + AAC  

## Pipeline

| Layer | Source |
|--------|--------|
| Visuals | Ken Burns on real `public/website/` Nexus module PNGs/JPGs + brand logo |
| On-screen text | PIL PNG overlays (header, scene title, compliance subtitles) |
| Watermark | `public/brand/sheikhgo-logo-white.png` (upper-right) |
| VO | `edge-tts` · `en-US-GuyNeural` · per-scene fit to timing via `atempo` + pad |
| Music | `docs/demo/audio-src/mixkit-motivating-mornings-33.mp3` @ ~14% under VO |
| Assembly | `build_promo.py` → ffmpeg concat + amix |

## Scene map (matches Ali brief)

| # | Time | Title |
|---|------|--------|
| 1 | 0:00–0:08 | Opening — aerial still → map → logo |
| 2 | 0:08–0:20 | Real-time GPS |
| 3 | 0:20–0:32 | Health, drivers, maintenance, AI assign |
| 4 | 0:32–0:44 | Fuel intelligence (suspected anomalies) |
| 5 | 0:44–0:54 | EV |
| 6 | 0:54–1:06 | Road safety |
| 7 | 1:06–1:14 | Document tracker |
| 8 | 1:14–1:22 | Customer ratings |
| 9 | 1:22–1:34 | AI Assistant (sample data; Ollama/DeepInfra) |
| 10 | 1:34–1:42 | SOS + Pakistan/Dubai (no guaranteed ETAs) |
| 11 | 1:42–1:50 | Reporting + secure video |
| 12 | 1:50–2:00 | CTA — sheikhgo.com · Book a Demo |

## Compliance notes

- **No fabricated stats or third-party customer logos** in the cut.
- **Website testimonial cards were not used** (site copy is illustrative; not shown in this promo).
- **AI scene** labeled on-screen as sample demo data; VO states Ollama/DeepInfra for production tenants.
- **Fuel** copy uses *suspected anomalies / investigate* language.
- **SOS** on-screen and VO: **no guaranteed ETAs**.
- **CTA URL:** https://www.sheikhgo.com only.

## Regenerate

```bash
cd Frontend/sheikhgo-erp/public/website/demo/promo-production
python3 build_promo.py
```

## Limitations / follow-ups

- **Grok Imagine B-roll** was unavailable in this run; opening “aerial fleet” uses licensed still `gps-alerts/hero-bg.jpg` with Ken Burns motion.
- Local **ffmpeg** build lacks `drawtext`/`libass`; text is PNG overlay (rebuild script if a full drawtext ffmpeg is installed).
- Optional **9:16 / 1:1** exports: crop center `1280x720` → `720x1280` or `720x720` with the same master audio.
