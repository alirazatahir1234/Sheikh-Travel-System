# SheikhGo Business Card — Production Assets

## Quality constraint (applied)

- **No AI redraw** of the card artwork. Masters are **byte-identical** extracts from Ali’s print PDF (`SheikhGo_Business_Card_Print.pdf`).
- **No JPEG recompression** of print faces. Front/back deliverables are **lossless PNG** at native **1121×671** (~**300 DPI** for the PDF mediabox ≈ **94.9 × 56.8 mm**).
- Chat attachments (359×215) are kept only as low-res references — **not** used for print.
- Logo reuse: `logo-sheikhgo-source-v8.png` / `logo-sheikh-source.svg` from `Frontend/sheikhgo-erp/public/brand/`.
- AI cue is an **additive SVG overlay only** (`sheikhgo-business-card-front-ai-cue.svg`) — does not alter the master PNG.

## Primary print set (same-to-same with sources 1 & 2 / PDF)

| File | Role |
|------|------|
| `SheikhGo_Business_Card_Print.pdf` | Original 2-page print PDF (canonical) |
| `sheikhgo-business-card-front.png` | Front — contact + dual QR (PDF page 1, native) |
| `sheikhgo-business-card-back.png` | Back — Solutions / Fleet & Software (PDF page 2, native) |
| `sheikhgo-business-card-front.svg` | Print wrapper embedding front PNG 1:1 |
| `sheikhgo-business-card-back.svg` | Print wrapper embedding back PNG 1:1 |

## Optional: validated QR overlay

| File | Role |
|------|------|
| `sheikhgo-business-card-front-real-qr.png` | Same front artwork; QR modules replaced with scannable codes (NEAREST resize — no blur) |
| `qr-website-sheikhgo.com.png` | Encodes `https://sheikhgo.com` |
| `qr-whatsapp-923177368305.png` | Encodes `https://wa.me/923177368305` |
| `qr-website-with-logo.png` / `qr-whatsapp-with-icon.png` | Same payloads + center brand/WA mark |

Use the **PDF / front.png / back.png** for print fidelity. Use **front-real-qr** when you need confirmed scan targets.

## Source mapping

1. **Contact/QR front** (chat `image-b4d0d50c…`) → matches PDF page 1 / `sheikhgo-business-card-front.png` (chat was downscaled; PDF native is the quality master).
2. **Solutions back** (chat `image-6dcad21a…`) → matches PDF page 2 / `sheikhgo-business-card-back.png`.
3. **Front+back mockup** (chat `image-a9ed2fa2…` / Downloads `SheikhGo Technologies Business Cards.png`) → `source-mockup-front-back-1536x1024.png` + `*-mockup-variant.png` halves (alternate photographic/glow layout; not the flat print PDF set).

## Subtle AI cue

`sheikhgo-business-card-front-ai-cue.svg` — embeds the untouched front PNG and adds a small teal **AI-POWERED** micro-label. Toggle by choosing SVG vs plain PNG.
