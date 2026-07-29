# SheikhGo Flagship Executive Presentation

**SheikhGo AI Fleet Operations Platform**  
*The Next Generation Intelligent Fleet Ecosystem*

Phase 1 deliverables: editable PowerPoint + PDF (60 slides).

## Rebuild

```bash
cd docs/presentation/flagship
npm install
npm run build
```

## Output

| File | Description |
|------|-------------|
| `output/SheikhGo-AI-Fleet-Operations-Platform.pptx` | Editable 60-slide deck |
| `output/SheikhGo-AI-Fleet-Operations-Platform.pdf` | Presentation-ready PDF |
| `output/SheikhGo-AI-Fleet-Operations-Platform.html` | Browser printable companion |

## Content posture

| Badge | Meaning |
|-------|---------|
| **Available Today** | Shipped in SheikhGo ERP / API / mobile |
| **Vision / Next-Gen** | Roadmap or partial AI |
| **Indicative** | Market / ROI ranges — not audited financials |

Edit copy in `content/slides.yaml`, then re-run `npm run build`.

## Brand

- Company: Sheikh Travel Group
- Colors: emerald `#0B6B50`, teal `#0f766e`, navy `#0f172a`
- Logos: `assets/sheikhgo-logo.png`, `assets/sheikhgo-logo-white.png`

## Optional PDF via browser

```bash
npx playwright install chromium
npm run pdf:browser
```

Legacy Python builder (`build_deck.py`) remains if `python-pptx` is installed.
