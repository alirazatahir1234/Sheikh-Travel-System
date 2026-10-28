# SheikhGo public website (inside ERP)

Marketing pages live in this module and are registered in `app-routing.module.ts`.

| Audience | URL |
|----------|-----|
| Guests | `/`, `/fleet-management`, `/gps-tracking`, `/vehicle-rental`, `/transportation-operations`, `/ai-fleet-insights`, `/features`, `/about`, `/contact`, `/request-demo`, `/fleet-assessment` |
| Everyone | `/live-tracking`, `/gps-alerts`, `/ai-solutions`, `/privacy-policy`, `/delete-account`, `/terms-and-conditions`, `/cookie-policy` |
| Auth | `/auth/login`, `/auth/forgot-password`, `/auth/reset-password` |

Signed-in users hitting `/` are redirected to `/dashboard`. Signed-in users get the ERP `/gps-tracking` module; guests get the marketing GPS page.

## Channel C — solution pages

Buyer-facing pages (target customer → problem → features → next step). Primary CTA **Book a Demo** (`/request-demo`); secondary **Request a Fleet Assessment** (`/fleet-assessment`).

| Page | Path |
|------|------|
| Fleet Management Software | `/fleet-management` |
| GPS Vehicle Tracking | `/gps-tracking` |
| Vehicle Rental Management | `/vehicle-rental` |
| Transportation Operations | `/transportation-operations` |
| AI Fleet Insights | `/ai-fleet-insights` |
| Book a Demo | `/request-demo` |
| Fleet Assessment | `/fleet-assessment` |

Copy source: `core/channel-c-solutions.ts`. Shared layout: `shared/solution-landing/`.

Rental page messaging is honest: ERP Vehicle Rental (rates/bookings/availability) plus fleet GPS — operator workspace, not a consumer marketplace.

## SEO

| Asset | Location |
|-------|----------|
| `robots.txt` | `public/robots.txt` → `https://www.sheikhgo.com/robots.txt` |
| `sitemap.xml` | `public/sitemap.xml` → `https://www.sheikhgo.com/sitemap.xml` |
| Default meta / OG | `src/index.html` |
| Per-route title, description, OG, canonical | `core/seo.service.ts` |

## Languages (EN · AR · DE)

Marketing chrome and primary page heroes use lightweight i18n (no Angular `$localize` yet):

| Piece | Location |
|-------|----------|
| Dictionaries | `core/i18n/messages.en.ts`, `messages.ar.ts`, `messages.de.ts` |
| Service | `core/website-i18n.service.ts` — `t(key)`, `setLanguage()`, `localStorage` key `sheikhgo.website.lang` |
| Pipe | `shared/website-translate.pipe.ts` — `{{ 'nav.home' \| wsTranslate }}` |
| Switcher | `shared/website-language-switcher/` — header desktop + mobile |

Arabic sets `document.documentElement.dir = 'rtl'` and `lang="ar"`. English remains the fallback for missing keys.

## Analytics (GA4 + optional Plausible)

Scripts load only when:

1. A valid ID/domain is configured, and  
2. The visitor accepts **analytics** cookies.

### Configure IDs (Ali)

| Variable | Example | Where |
|----------|---------|--------|
| `GOOGLE_ANALYTICS_MEASUREMENT_ID` | `G-XXXXXXXXXX` | Vercel / CI env, or `.env.local`, or Website CMS → Analytics ID |
| `PLAUSIBLE_DOMAIN` | `www.sheikhgo.com` | Vercel / CI / `.env.local` (hostname only) |
| `PLAUSIBLE_SCRIPT_SRC` | optional | Self-hosted Plausible script URL |

Build inject: `scripts/inject-maps-env.mjs` patches `environment*.ts` then restores empty placeholders after build — **never commit live measurement IDs**.

Local: copy `.env.example` → `.env.local` or `environment.secrets.local.ts.example` → `environment.secrets.local.ts` (gitignored).

**GA4:** [Google Analytics](https://analytics.google.com/) → Admin → property for `www.sheikhgo.com` → Web data stream → Measurement ID (`G-…`).

**Plausible:** add site `www.sheikhgo.com`, set `PLAUSIBLE_DOMAIN=www.sheikhgo.com` on Vercel Production.

### Conversion events (before paid traffic)

| Event | When |
|-------|------|
| `cta_book_demo_click` | Book a Demo CTA on solution pages |
| `cta_fleet_assessment_click` | Fleet Assessment CTA on solution pages |
| `demo_request_submit` | Successful `/request-demo` form |
| `fleet_assessment_submit` | Successful `/fleet-assessment` form |
| `mobile_promo_video_open` / `mobile_app_store_click` | Existing mobile promo |

Events fire to GA4 (`gtag`) and Plausible when each provider is enabled.
