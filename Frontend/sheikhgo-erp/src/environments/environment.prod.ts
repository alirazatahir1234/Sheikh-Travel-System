export const environment = {
  production: true,
  /**
   * Stable HTTPS API via Caddy → Kestrel on Azure VM (sslip.io).
   * Do NOT point production at ephemeral trycloudflare.com tunnels — they die on restart
   * and break login from https://www.sheikhgo.com (Mixed Content / dead origin).
   * Preferred after DNS: https://api.sheikhgo.com/api
   */
  apiUrl: 'https://20-174-1-230.sslip.io/api',
  /** Same host as SPA; absolute API origin used for /uploads when filesBaseUrl is empty. */
  filesBaseUrl: '',
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
