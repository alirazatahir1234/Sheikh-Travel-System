# Pakistan single-tracker annual subscription — market research

Research date: October 2026. Used to set SheikhGo Nexus **per-tracker annual** list prices (software/platform; device and install excluded).

## 1. Market range (PKR / year / tracker)

| Provider | Plan / note | Annual platform or AMC (PKR) | Source |
| --- | --- | --- | --- |
| GoTrack | Basic / Standard / Gold AMC | 8,000 / 9,000 / 10,000 | [gotrack.com.pk/pricing](https://gotrack.com.pk/pricing) |
| Sky Track | Affordable / Economy / Luxury next-year AMC | 8,000 / 9,000 / 10,000 | [skytrackpk.com/packages](https://skytrackpk.com/packages/) |
| Sky Track | Personal self-monitoring AMC | 4,600 | [skytrackpk.com/packages](https://skytrackpk.com/packages/) |
| Z Track | Basic / Professional (800 / 1,400 PKR per device per month) | ~9,600 / ~16,800 implied annual | [ztrack.com.pk/pricing](https://www.ztrack.com.pk/pricing) |
| AlphaTrack (aggregator) | Basic / Standard / Premium next-year AMF | 8,000 / 10,000 / 12,000 | [alphatrack.pk](https://alphatrack.pk/car-tracker-price-in-pakistan-installation-annual-charges/) |

**Observed band:** roughly **PKR 4,600 – 12,000 / year** for one tracker (self-monitoring low end; monitored mainstream **PKR 8,000 – 10,000**).

## 2. Cost-based floor (SheikhGo single tracker / year)

Incremental COGS per active tracker (Maps/Roads geocoding, SMS alert volume, share of Azure/Vercel) is estimated at roughly **PKR 5,500 – 7,000 / year** at moderate SMS usage — derived from internal fleet ops band (~PKR 21k–83k/month for 20–50 vehicles) allocated per device, not as a customer list price.

## 3. Recommended SheikhGo list prices

Aligned with GoTrack / SkyTrack AMC tiers (competitive, covers incremental platform cost):

| Code | Display | PKR / year |
| --- | --- | --- |
| `TRACKER_ESSENTIAL` | Essential | 8,000 |
| `TRACKER_PLUS` | Plus (default) | 9,000 |
| `TRACKER_PREMIUM` | Premium | 10,000 |

## 4. Caveats

- Local quotes often bundle **device + first-year monitoring**; our prices are **platform subscription only** (BYOD or partner-installed hardware).
- SMS/WhatsApp volume above included fair-use may need bundles (same as fleet Nexus add-ons).
- Taxes, payment gateway fees, and custom SLAs are quoted separately.
