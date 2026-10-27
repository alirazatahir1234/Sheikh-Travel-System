# SheikhGo demo assets

| Asset | Path |
|-------|------|
| **ERP walkthrough video (~3 min, Watch Demo primary)** | `Frontend/sheikhgo-erp/public/website/demo/sheikhgo-nexus-erp-walkthrough.mp4` |
| **ERP recording manifest** | `Frontend/sheikhgo-erp/public/website/demo/sheikhgo-nexus-erp-walkthrough.manifest.json` |
| **Nexus walkthrough video (~2:57, marketing + ERP montage)** | `Frontend/sheikhgo-erp/public/website/demo/sheikhgo-nexus-walkthrough.mp4` |
| **Recording manifest** | `Frontend/sheikhgo-erp/public/website/demo/sheikhgo-nexus-walkthrough.manifest.json` |
| **ERP app narrated / shot list (~3 min)** | [SheikhGo-Nexus-ERP-Walkthrough-Script.md](./SheikhGo-Nexus-ERP-Walkthrough-Script.md) |
| **Marketing site narrated script (6–8 min)** | [SheikhGo-Nexus-Walkthrough-Script.md](./SheikhGo-Nexus-Walkthrough-Script.md) |
| **iOS mobile walkthrough video (automated Simulator)** | `Frontend/sheikhgo-erp/public/website/demo/sheikhgo-nexus-ios-walkthrough.mp4` |
| **iOS recording manifest** | `Frontend/sheikhgo-erp/public/website/demo/sheikhgo-nexus-ios-walkthrough.manifest.json` |
| **iOS narrated script (5–7 min)** | [SheikhGo-Nexus-iOS-Walkthrough-Script.md](./SheikhGo-Nexus-iOS-Walkthrough-Script.md) |
| **Slide deck outline** | [SheikhGo-Demo-Slides.md](./SheikhGo-Demo-Slides.md) |

## Background music (demo MP4s)

All three walkthrough MP4s above include a mixed **AAC** stereo bed (no separate narration track yet).

| Field | Value |
|-------|--------|
| **Track** | [Motivating Mornings](https://mixkit.co/free-stock-music/download/33/) (Mixkit item #33) by Ahjay Stelino |
| **Source file (repo)** | `docs/demo/audio-src/mixkit-motivating-mornings-33.mp3` |
| **License** | [Mixkit Free License](https://mixkit.co/license/) — royalty-free; commercial use allowed; attribution not required |
| **Mix level** | ~22% of source RMS (`volume=0.22` in ffmpeg) |
| **Fades** | 2 s fade-in; 3 s fade-out; track looped to match each video duration |

Re-apply after regenerating a silent recording:

```bash
AUDIO=docs/demo/audio-src/mixkit-motivating-mornings-33.mp3
VOL=0.22
mix_bgm() {
  local IN="$1" OUT="$2"
  local DUR FADE_START
  DUR=$(ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 "$IN")
  FADE_START=$(python3 -c "print(max(0, float('$DUR') - 3))")
  ffmpeg -y -i "$IN" -stream_loop -1 -i "$AUDIO" \
    -filter_complex "[1:a]volume=${VOL},afade=t=in:st=0:d=2,atrim=0:${DUR},afade=t=out:st=${FADE_START}:d=3[aout]" \
    -map 0:v:0 -map "[aout]" -c:v copy -c:a aac -b:a 192k -movflags +faststart "$OUT"
}
DEMO=Frontend/sheikhgo-erp/public/website/demo
for f in sheikhgo-nexus-erp-walkthrough sheikhgo-nexus-ios-walkthrough sheikhgo-nexus-walkthrough; do
  mix_bgm "$DEMO/${f}.mp4" "/tmp/${f}-bgm.mp4" && mv "/tmp/${f}-bgm.mp4" "$DEMO/${f}.mp4"
done
```

ERP walkthrough mixing is built into `record-nexus-erp-demo.mjs` (`BGM_VOLUME` default `0.22`). Set `SKIP_BGM=1` to export silent video only.

## Regenerate the ERP app walkthrough

From repo root, with `ng serve` on port 4200:

```bash
cd Frontend/sheikhgo-erp
node tools/record-nexus-erp-demo.mjs
```

Stitch + BGM only (after segments were recorded):

```bash
STITCH_ONLY=1 node tools/record-nexus-erp-demo.mjs
```

## Regenerate the marketing site video

From repo root, with `ng serve` on port 4200:

```bash
cd Frontend/sheikhgo-erp
node tools/record-nexus-demo.mjs
```

Stitch only (after a failed ffmpeg step):

```bash
STITCH_ONLY=1 node tools/record-nexus-demo.mjs
```

Refresh module PNGs on the home gallery:

```bash
node tools/capture-nexus-modules.mjs
```

## Regenerate the iOS mobile video

Requires Xcode Simulator booted and network access to the prod demo API:

```bash
cd Frontend/sheikh-driver
chmod +x tools/record-ios-mobile-demo.sh
./tools/record-ios-mobile-demo.sh
```

## Play & share

- **Local:** open the MP4 in QuickTime/VLC, or `open public/website/demo/sheikhgo-nexus-walkthrough.mp4`
- **On site (after deploy):** `https://www.sheikhgo.com/website/demo/sheikhgo-nexus-walkthrough.mp4`
- **Sales:** upload to Google Drive / YouTube / LinkedIn; pair with the narrated script for voiceover
- **Home hero:** optional — point `hero-demo.mp4` at this file if you want the homepage modal to use the full walkthrough
