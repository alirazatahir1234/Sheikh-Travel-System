export const environment = {
  production: true,
  /**
   * Fallback only. The real API host is read at startup from assets/config.json
   * (src/config/production/config.json, see src/config/README.md) — never bake a host here.
   */
  apiUrl: '/api',
  /** Same host as SPA; absolute API origin used for /uploads when filesBaseUrl is empty. */
  filesBaseUrl: '',
  /**
   * Marketing media CDN origin (no trailing slash).
   * Cloudflare Worker proxies Azure Blob (same /website-images|website-videos paths).
   * Do not point at blob.core.windows.net directly; custom media.sheikhgo.com DNS is later.
   */
  websiteMediaBaseUrl: 'https://sheikhgo-media.sheikhgo.workers.dev',
  tenantSlug: 'default',
  // Injected at build by scripts/inject-maps-env.mjs from GOOGLE_MAPS_API_KEY /
  // environment.secrets.local.ts / .env.local — never commit a live key here.
  // Restrict browser keys by HTTP referrer in Google Cloud Console.
  googleMapsApiKey: '',
  googleMapsMapId: '',
  /** Injected at build via GOOGLE_ANALYTICS_MEASUREMENT_ID (see scripts/inject-maps-env.mjs). */
  googleAnalyticsMeasurementId: '',
  /** Injected at build via PLAUSIBLE_DOMAIN — leave empty until configured. */
  plausibleDomain: '',
  /** Injected at build via PLAUSIBLE_SCRIPT_SRC (optional self-hosted script). */
  plausibleScriptSrc: '',
};
