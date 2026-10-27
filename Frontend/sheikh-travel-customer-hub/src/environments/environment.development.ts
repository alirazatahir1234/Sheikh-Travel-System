export const environment = {
  production: false,
  /** Dev server uses `proxy.conf.json` to forward `/api` to the API (default http://127.0.0.1:5082). */
  apiRoot: '/api',
  hubRoot: 'http://127.0.0.1:5082',
  /** Leave empty in git — use environment.secrets.local.ts (gitignored) or paste for local only. */
  googleMapsApiKey: '',
  paymentGatewayEnabled: false,
  whatsAppNumber: '923001234567'
};
