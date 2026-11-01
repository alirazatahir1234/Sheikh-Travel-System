export const environment = {
  production: true,
  /** HTTPS via Cloudflare quick tunnel. After DNS A `api` → 20.174.1.230, use https://api.sheikhgo.com. */
  apiRoot: 'https://joke-rebates-titans-duties.trycloudflare.com/api',
  // Inject at build time (CI secret / file replace). Local: environment.secrets.local.ts (gitignored).
  googleMapsApiKey: '',
  /** SignalR hub base (no /api suffix). */
  hubRoot: 'https://joke-rebates-titans-duties.trycloudflare.com',
  /** When true, online card payments show gateway placeholder UI. */
  paymentGatewayEnabled: false,
  whatsAppNumber: '923001234567'
};
