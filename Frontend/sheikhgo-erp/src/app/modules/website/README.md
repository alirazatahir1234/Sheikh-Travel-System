# SheikhGo public website (inside ERP)

Marketing pages live in this module and are registered in `app-routing.module.ts`.

| Audience | URL |
|----------|-----|
| Guests | `/` Home, `/fleet-management`, `/gps-tracking`, `/features`, `/about`, `/contact`, `/request-demo` |
| Everyone | `/live-tracking`, `/gps-alerts`, `/privacy-policy`, `/delete-account`, `/terms-and-conditions`, `/cookie-policy` |
| Auth | `/auth/login`, `/auth/forgot-password`, `/auth/reset-password` |

Signed-in users hitting `/` are redirected to `/dashboard`. Signed-in users get the ERP `/gps-tracking` module; guests get the marketing GPS page.

## Languages (EN · AR · DE)

Marketing chrome and primary page heroes use lightweight i18n (no Angular `$localize` yet):

| Piece | Location |
|-------|----------|
| Dictionaries | `core/i18n/messages.en.ts`, `messages.ar.ts`, `messages.de.ts` |
| Service | `core/website-i18n.service.ts` — `t(key)`, `setLanguage()`, `localStorage` key `sheikhgo.website.lang` |
| Pipe | `shared/website-translate.pipe.ts` — `{{ 'nav.home' \| wsTranslate }}` |
| Switcher | `shared/website-language-switcher/` — header desktop + mobile |

Arabic sets `document.documentElement.dir = 'rtl'` and `lang="ar"`. English remains the fallback for missing keys.

**Coverage:** Full header/footer/mega-menu, home hero + major sections, and heroes/CTAs on `/live-tracking`, `/gps-alerts`, `/ai-solutions`, `/services`, `/contact`, `/request-demo`. Deeper body copy (capability grids, legal pages, demo chat bubbles) is still English until keys are added.

## Google Analytics (GA4)

Marketing pages load **gtag.js** only when:

1. A valid GA4 measurement ID is configured (`G-XXXXXXXXXX`), and  
2. The visitor accepts **analytics** cookies.

**Configure the measurement ID (pick one):**

| Method | Where |
|--------|--------|
| Vercel / CI env | `GOOGLE_ANALYTICS_MEASUREMENT_ID=G-XXXXXXXXXX` (patched at build by `scripts/inject-maps-env.mjs`) |
| Angular env | `googleAnalyticsMeasurementId` in `src/environments/environment.ts` (local dev) or `environment.prod.ts` |
| CMS | Website Admin → Settings → **Analytics ID** (overrides env when set) |

**Create a property:** [Google Analytics](https://analytics.google.com/) → Admin → Create property for `www.sheikhgo.com` → Data stream (Web) → copy the **Measurement ID** (`G-…`).

Default homepage SEO tags live in `src/index.html`; per-route titles/descriptions/Open Graph are updated by `core/seo.service.ts` when marketing pages load.
