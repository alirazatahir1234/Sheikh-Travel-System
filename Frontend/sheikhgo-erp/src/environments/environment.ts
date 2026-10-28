import { mapsSecrets } from './environment.secrets.local';

export const environment = {
  production: false,
  // Proxied to http://127.0.0.1:5082 via proxy.conf.json when using ng serve
  apiUrl: '/api',
  /** Absolute base for uploaded files (API origin). Avoids resolving /uploads on :4200. */
  filesBaseUrl: 'http://127.0.0.1:5082',
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
};
