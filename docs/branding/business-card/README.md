# SheikhGo Business Card — Production Assets

## Quality constraint (applied)

- **No AI redraw** of the card artwork. Masters are **byte-identical** embeds extracted from Ali’s **second** print PDF.
- **No JPEG recompression** of print faces. Front/back deliverables are **lossless PNG** at native **1121×671** (~**300 DPI** for the PDF mediabox ≈ **94.9 × 56.8 mm**).
- Chat attachments (359×215) are kept only as low-res references — **not** used for print.
- Logo / vector companions from the Codex export are included for print shops that prefer SVG.

## Canonical source (second PDF)

| File | Role |
|------|------|
| `SheikhGo_Business_Card_Print.pdf` | **Second** print PDF (Codex export 2026-11-01 12:21, MD5 `117005fd…`) — **canonical** |
| `SheikhGo_Business_Card_Print_first.pdf` | First print PDF (Downloads 12:11) — archived for comparison only; **do not print** |

## Primary print set (exact second-PDF extract)

| File | Role |
|------|------|
| `sheikhgo-business-card-front.png` | Front — photo + contact + dual QR (PDF page 1 embed, byte-identical) |
| `sheikhgo-business-card-back.png` | Back — Solutions / navy sidebar (PDF page 2 embed, byte-identical) |
| `sheikhgo-business-card-front.svg` | Print wrapper embedding front PNG 1:1 |
| `sheikhgo-business-card-back.svg` | Print wrapper embedding back PNG 1:1 |
| `sheikhgo-business-card-front-vector.svg` / `sheikhgo-business-card-back-vector.svg` | Vector companions from the same export |
| `source-codex-front-300dpi.png` / `source-codex-back-300dpi.png` | Companion 300 DPI renders from the same export |
| `source-presentation-1536x1024.png` | Presentation mock of front+back from the same export |

## Optional: validated QR overlay

| File | Role |
|------|------|
| `sheikhgo-business-card-front-real-qr.png` | Same second-PDF front; QR modules replaced with scannable codes (NEAREST — no blur) |
| `qr-website-sheikhgo.com.png` | Encodes `https://sheikhgo.com` |
| `qr-whatsapp-923177368305.png` | Encodes `https://wa.me/923177368305` |

Use the **PDF / front.png / back.png** for print fidelity. Use **front-real-qr** when you need confirmed scan targets.

## What changed vs the first PDF

The first PDF was a flat dual-QR teal card (`SMART FLEET. SMARTER OPERATIONS.`, full-bleed solutions back). The **second** PDF is a different layout: front has highway bus/car photo + `TRACK • MANAGE • MOVE` + icon service row; back is light panels + navy “CONNECTED MOBILITY” sidebar with map pins.

## Subtle AI cue (optional)

`sheikhgo-business-card-front-ai-cue.svg` — embeds the untouched front PNG and adds a small teal **AI-POWERED** micro-label. Toggle by choosing SVG vs plain PNG.
