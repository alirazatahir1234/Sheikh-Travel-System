export const environment = {
  production: true,
  /**
   * HTTPS via Caddy/sslip (Let’s Encrypt). Dead trycloudflare quick tunnels must
   * not ship here. After DNS A `api` → 20.174.1.230, use https://api.sheikhgo.com.
   */
  apiRoot: 'https://20-174-1-230.sslip.io/api',
  // Inject at build time (CI secret / file replace). Local: environment.secrets.local.ts (gitignored).
  googleMapsApiKey: '',
  /** SignalR hub base (no /api suffix). */
  hubRoot: 'https://20-174-1-230.sslip.io',
  /** When true, online card payments show gateway placeholder UI. */
  paymentGatewayEnabled: false,
  whatsAppNumber: '923001234567'
};
