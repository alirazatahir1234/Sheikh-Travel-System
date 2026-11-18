import { mapsSecrets } from './environment.secrets.local';

export const environment = {
  production: false,
  /** SG-22: MCP Console (/mcp) — on in local/dev; off in production builds. */
  enableMcpConsole: true,
  // Proxied to http://127.0.0.1:5082 via proxy.conf.json when using ng serve
  apiUrl: '/api',
  /** Absolute base for uploaded files (API origin). Avoids resolving /uploads on :4200. */
  filesBaseUrl: 'http://127.0.0.1:5082',
  /**
   * Marketing media CDN origin (no trailing slash).
   * Paths resolve to {base}/website-images|website-videos/...
   * Cloudflare Worker proxies Azure Blob; leave empty to use local /website/.
   */
  websiteMediaBaseUrl: 'https://sheikhgo-media.sheikhgo.workers.dev',
  tenantSlug: 'default',
  // Merged from gitignored environment.secrets.local.ts so bare `ng s` works
  // without npm start. Copy from environment.secrets.local.ts.example if missing
  // (`npm start` / `npm run ng` also run ensure + inject-maps-env --dev).
  // Enable: Maps JavaScript API, Places API, Geocoding API, Routes API.
  // Restrict by HTTP referrer (localhost + sheikhgo.com) — never commit live keys.
  googleMapsApiKey: mapsSecrets.googleMapsApiKey || '',
  googleMapsMapId: mapsSecrets.googleMapsMapId || '',
  /**
   * GA4 measurement ID (e.g. G-XXXXXXXXXX). Leave empty until configured in Google Analytics.
   * Production builds: set GOOGLE_ANALYTICS_MEASUREMENT_ID or Website CMS → Analytics ID.
   */
  googleAnalyticsMeasurementId: mapsSecrets.googleAnalyticsMeasurementId || '',
  /**
   * Plausible site domain (e.g. www.sheikhgo.com). Leave empty until configured.
   * Production: PLAUSIBLE_DOMAIN via inject-maps-env / Vercel env — never commit a live requirement.
   */
  plausibleDomain: mapsSecrets.plausibleDomain || '',
  /** Optional custom Plausible script URL (self-hosted). Default: https://plausible.io/js/script.js */
  plausibleScriptSrc: mapsSecrets.plausibleScriptSrc || ''
};
