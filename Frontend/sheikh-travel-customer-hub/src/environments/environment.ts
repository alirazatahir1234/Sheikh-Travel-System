/** Local `ng serve` replaces this file with `environment.development.ts` (proxy `/api`). */
export const environment = {
  production: false,
  apiRoot: '/api',
  hubRoot: 'http://127.0.0.1:5082',
  /** Leave empty in git — copy environment.secrets.local.ts.example or set GOOGLE_MAPS_API_KEY for local. */
  googleMapsApiKey: '',
  paymentGatewayEnabled: false,
  whatsAppNumber: '923001234567'
};
