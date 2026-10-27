# SheikhGo demo assets

| Asset | Path |
|-------|------|
| **Nexus walkthrough video (~2–6 min, silent + title cards)** | `Frontend/sheikhgo-erp/public/website/demo/sheikhgo-nexus-walkthrough.mp4` |
| **Recording manifest** | `Frontend/sheikhgo-erp/public/website/demo/sheikhgo-nexus-walkthrough.manifest.json` |
| **Narrated script (6–8 min)** | [SheikhGo-Nexus-Walkthrough-Script.md](./SheikhGo-Nexus-Walkthrough-Script.md) |
| **Slide deck outline** | [SheikhGo-Demo-Slides.md](./SheikhGo-Demo-Slides.md) |

## Regenerate the video

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

## Play & share

- **Local:** open the MP4 in QuickTime/VLC, or `open public/website/demo/sheikhgo-nexus-walkthrough.mp4`
- **On site (after deploy):** `https://www.sheikhgo.com/website/demo/sheikhgo-nexus-walkthrough.mp4`
- **Sales:** upload to Google Drive / YouTube / LinkedIn; pair with the narrated script for voiceover
- **Home hero:** optional — point `hero-demo.mp4` at this file if you want the homepage modal to use the full walkthrough
