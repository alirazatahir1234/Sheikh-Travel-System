export const environment = {
  production: false,
  // Proxied to http://127.0.0.1:5082 via proxy.conf.json when using ng serve
  apiUrl: '/api',
  /** Absolute base for uploaded files (API origin). Avoids resolving /uploads on :4200. */
  filesBaseUrl: 'http://127.0.0.1:5082',
  tenantSlug: 'default',
  // Google Maps JavaScript API key — leave empty in git.
  // Local: `npm start` runs inject-maps-env --dev from GOOGLE_MAPS_API_KEY /
  // .env.local / environment.secrets.local.ts (see .env.example).
  // Enable: Maps JavaScript API, Places API, Geocoding API, Routes API.
  // Restrict by HTTP referrer (localhost + sheikhgo.com) — never commit live keys.
  googleMapsApiKey: '',
  googleMapsMapId: '',
  /**
   * GA4 measurement ID (e.g. G-XXXXXXXXXX). Leave empty until configured in Google Analytics.
   * Production builds: set GOOGLE_ANALYTICS_MEASUREMENT_ID or Website CMS → Analytics ID.
   */
  googleAnalyticsMeasurementId: '',
};
